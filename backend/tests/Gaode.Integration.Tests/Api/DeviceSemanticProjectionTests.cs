using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class DeviceSemanticProjectionTests
{
    [Theory]
    [InlineData("API-OBS/reliable", DeviceReliability.Reliable)]
    [InlineData("API-OBS/stale", DeviceReliability.Stale)]
    [InlineData("API-OBS/unavailable", DeviceReliability.Unavailable)]
    public async Task StatusPublishesOnlyRegisteredSemanticFieldsAndActualSamples(string caseId, DeviceReliability reliability)
    {
        var observation = Observation(reliability);
        await using var host = await Station01HostFixture.CreateAsync(services => {
            services.RemoveAll<IPlcStatePort>(); services.AddSingleton<IPlcStatePort>(new StatePort(observation));
        });
        using var response = await host.Client.GetAsync("/api/v1/station01/status");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("s01-status/2.0", root.GetProperty("schemaVersion").GetString());
        var plc = root.GetProperty("plc");
        if (reliability == DeviceReliability.Unavailable) { Assert.Equal(JsonValueKind.Null, plc.ValueKind); return; }
        string[] expected = ["schemaVersion", "reliability", "connection", "connectionEpoch", "operatingMode", "readiness",
            "safetyAssessment", "clamp", "motionAvailability", "acquisitionReadiness", "manualArea", "manualHandling",
            "position", "alarms", "reasonCodes", "executionOrigin", "observationId", "sampleStartedUtc", "sampleEndedUtc", "diagnosticEvidenceReference", "axisObservations"];
        Assert.Equal(expected.Order(), plc.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(reliability.ToString(), plc.GetProperty("reliability").GetString());
        Assert.Equal(observation.Identity!.ObservationId, plc.GetProperty("observationId").GetGuid());
        Assert.Equal(12, plc.GetProperty("position").GetProperty("actualX").GetDouble());
        Assert.Equal("device-semantics/1.2", plc.GetProperty("schemaVersion").GetString());
        var position = plc.GetProperty("position");
        Assert.Equal(observation.Position!.Identity.SampleStartedUtc, position.GetProperty("sampleStartedUtc").GetDateTimeOffset());
        Assert.Equal(observation.Position.Identity.SampleEndedUtc, position.GetProperty("sampleEndedUtc").GetDateTimeOffset());
        Assert.Equal(observation.Position.Identity.ConnectionEpoch, position.GetProperty("connectionEpoch").GetInt64());
        Assert.Equal(observation.Position.Identity.Reliability.ToString(), position.GetProperty("reliability").GetString());
        var axes = plc.GetProperty("axisObservations").EnumerateArray().ToDictionary(a => a.GetProperty("axis").GetString()!);
        Assert.Equal(new[] { "X", "Y", "CameraZ", "ScanZ", "GrabZ" }.Order(), axes.Keys.Order());
        Assert.Equal(36, axes["CameraZ"].GetProperty("position").GetDouble());
        Assert.Equal(48, axes["ScanZ"].GetProperty("position").GetDouble());
        Assert.Equal(60, axes["GrabZ"].GetProperty("position").GetDouble());
        Assert.All(axes.Values, a => Assert.Equal(reliability.ToString(), a.GetProperty("reliability").GetString()));
        Assert.Equal("Virtual", plc.GetProperty("executionOrigin").GetProperty("provider").GetString());
        Assert.Equal(reliability == DeviceReliability.Reliable ? JsonValueKind.Array : JsonValueKind.Null, plc.GetProperty("alarms").ValueKind);
        Assert.Equal(0, host.Plc.StartCommands);
        Assert.StartsWith("API-OBS/", caseId);
    }

    private sealed class StatePort(DeviceObservation observation) : IPlcStatePort { public DeviceObservation Observe() => observation; }

    [Fact]
    public async Task StartupProjectionKeepsBusinessStateAndSameObservationWithoutInventingRaw()
    {
        var observed = Observation(DeviceReliability.Reliable);
        await using var host = await Station01HostFixture.CreateAsync();
        var coordinator = host.Host.Services.GetRequiredService<Station01Coordinator>();
        var runId = Guid.NewGuid();
        var snapshot = new RunSnapshot(runId, "semantic-api-component", "test:Operator", RunState.Blocked,
            1, 0, TerminalOutcome.None, false, ActionState.NotRequested, CaptureState.NotRequested,
            AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady, null, null, null, [],
            StartupDiagnostic: new(["DeviceNotReady"], "Other", "BeforeStart", "BlockedNoDeviceAction",
                observed.ConnectionEpoch, observed.Identity!.SampleEndedUtc, observed.ExecutionOrigin, observed));
        Assert.True(coordinator.TryRegister(snapshot));
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync($"/api/v1/station01/runs/{runId:D}"));
        var body = document.RootElement;
        Assert.Equal((int)RunState.Blocked, body.GetProperty("state").GetInt32());
        Assert.Equal("Blocked", body.GetProperty("executionState").GetString());
        Assert.Equal("device-semantics/1.2", body.GetProperty("deviceSchemaVersion").GetString());
        var diagnostic = body.GetProperty("startupDiagnostic");
        Assert.Equal("device-semantics/1", diagnostic.GetProperty("schemaVersion").GetString());
        Assert.Equal("Derived", diagnostic.GetProperty("recordNature").GetString());
        Assert.Equal("RawUnavailable", diagnostic.GetProperty("rawAvailability").GetString());
        Assert.Equal("Other", diagnostic.GetProperty("safetyAssessment").GetString());
        Assert.Equal("Clear", diagnostic.GetProperty("semanticObservation").GetProperty("safetyAssessment").GetString());
        Assert.Equal(observed.Identity.ObservationId, diagnostic.GetProperty("semanticObservation").GetProperty("observationId").GetGuid());
        Assert.Equal(JsonValueKind.Null, diagnostic.GetProperty("diagnosticEvidenceReference").ValueKind);
        // Read the published API shape, then preserve it as evidence. The flat
        // position has an observationId, not an internal Identity/Origin object.
        var consumer = body.Deserialize<RunApiSnapshot>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(observed.Identity.ObservationId, consumer.StartupDiagnostic!.SemanticObservation!.Position!.ObservationId);
        Assert.Equal(observed.Identity.SampleEndedUtc, consumer.StartupDiagnostic.SemanticObservation.SampleEndedUtc);
        Assert.Equal(observed.ExecutionOrigin, consumer.StartupDiagnostic.SemanticObservation.ExecutionOrigin);
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(consumer, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(
            System.Text.Json.Nodes.JsonNode.Parse(diagnostic.GetRawText()),
            System.Text.Json.Nodes.JsonNode.Parse(saved.RootElement.GetProperty("startupDiagnostic").GetRawText())));
        Assert.Equal(0, host.Plc.StartCommands);
    }
    private static DeviceObservation Observation(DeviceReliability reliability)
    {
        var now = DateTimeOffset.UtcNow;
        var identity = reliability == DeviceReliability.Unavailable ? null : new ObservationIdentity(Guid.NewGuid(), 4, now.AddMilliseconds(-7), now, reliability);
        var origin = new ExecutionOrigin(DeviceProvider.Virtual, "semantic-fixture/1", EvidenceQuality.Derived);
        return new(reliability, DeviceConnection.Connected, 4, OperatingMode.Automatic, DeviceReadiness.Ready,
            SafetyAssessment.Clear, ClampState.Released, MotionAvailability.Available, AcquisitionReadiness.Available,
            ManualAreaState.Clear, ManualHandlingState.Unconfirmed,
            identity is null ? null : new(12, 24, 36, "TestMachineAxes", "SIM_MACHINE", "Test:mm", identity, origin), null,
            [new("KnownAlarm", AlarmLevel.Warning, reliability)], [], origin, identity)
            { AxisPositions = identity is null ? null : new(12, 24, 36, 48, 60) };
    }
}
