using System.Net;
using System.Net.Http.Json;
using Gaode.Application.Station01;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Headers;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class StartQueryTests
{
    [Theory]
    [InlineData("Operator", HttpStatusCode.Accepted)]
    [InlineData("EquipmentEngineer", HttpStatusCode.Forbidden)]
    [InlineData("ProcessEngineer", HttpStatusCode.Forbidden)]
    [InlineData("SystemAdministrator", HttpStatusCode.Accepted)]
    public async Task ExplicitRoleTokensEnforceStartPermission(string role, HttpStatusCode expected)
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        using var client = fixture.ClientForRole(role);
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var response = await client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden) Assert.Equal(0, fixture.Plc.StartCommands);
    }

    [Theory]
    [InlineData("Operator")]
    [InlineData("EquipmentEngineer")]
    [InlineData("ProcessEngineer")]
    [InlineData("SystemAdministrator")]
    public async Task AllDefinedRolesCanReadStatusAndRoleHeaderCannotImpersonate(string role)
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        using var client = fixture.ClientForRole(role);
        var response = await client.GetAsync("/api/v1/station01/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        using var anonymous = new HttpClient(fixture.Host.Server.CreateHandler())
        { BaseAddress = fixture.Client.BaseAddress };
        anonymous.DefaultRequestHeaders.Add("X-Role", role);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/station01/status")).StatusCode);
    }

    [Fact]
    public async Task QueryEtagsAreStableAndChangeWithRunRevision()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var start = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        var receipt = (await start.Content.ReadFromJsonAsync<StartReceipt>())!;
        var command = await fixture.Client.GetAsync($"/api/v1/station01/commands/{receipt.CommandId}");
        Assert.NotNull(command.Headers.ETag);
        HttpResponseMessage? first = null;
        var until = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < until)
        {
            first = await fixture.Client.GetAsync(receipt.StatusUrl);
            var state = await first.Content.ReadFromJsonAsync<RunApiSnapshot>();
            if (state?.State == RunState.WaitingClamp) break;
            await Task.Delay(20);
        }
        Assert.NotNull(first?.Headers.ETag);
        using var conditional = new HttpRequestMessage(HttpMethod.Get, receipt.StatusUrl);
        conditional.Headers.IfNoneMatch.Add(first!.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified,
            (await fixture.Client.SendAsync(conditional)).StatusCode);
        string? changedTag = null;
        while (DateTimeOffset.UtcNow < until)
        {
            var current = await fixture.Client.GetAsync(receipt.StatusUrl);
            if (current.Headers.ETag?.Tag != first.Headers.ETag!.Tag)
            { changedTag = current.Headers.ETag?.Tag; break; }
            await Task.Delay(20);
        }
        Assert.NotNull(changedTag);

        var settleUntil = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < settleUntil)
        {
            var settled = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
            if (settled?.State is RunState.Completed or RunState.CompletedWithExceptions or
                RunState.Blocked or RunState.ConfigurationBlocked or RunState.RecoveryRequired) break;
            await Task.Delay(20);
        }

        var status = await fixture.Client.GetAsync("/api/v1/station01/status");
        Assert.NotNull(status.Headers.ETag);
        using var conditionalStatus = new HttpRequestMessage(HttpMethod.Get, "/api/v1/station01/status");
        conditionalStatus.Headers.IfNoneMatch.Add(status.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified,
            (await fixture.Client.SendAsync(conditionalStatus)).StatusCode);
    }
    [Fact]
    public async Task InvalidPublicReferencePersistsContextAndDoesNotRequestPlc()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var request = new StartPublicRequest("invalid-config-1", StartRunContextJson.Create(),
            new("missing-public", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        RunApiSnapshot? snapshot = null;
        var until = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < until)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>("/api/v1/station01/runs/" + receipt.RunId);
            if (snapshot?.State is RunState.ConfigurationBlocked or RunState.Blocked) break;
            await Task.Delay(20);
        }
        Assert.Equal(RunState.ConfigurationBlocked, snapshot?.State);
        Assert.Equal("ConfigurationNotFound", snapshot?.ErrorCode);
        Assert.Equal(0, fixture.Plc.StartCommands);
        var query = fixture.Host.Services.GetRequiredService<ITraceQuery>();
        var persisted = await query.GetRunAsync(receipt.RunId, default);
        Assert.NotNull(persisted);
        Assert.Equal(request.ContextJson, persisted.ContextJson);
        Assert.Equal(RunState.ConfigurationBlocked, persisted.State);
        Assert.Contains(await query.GetWritesAsync(receipt.RunId, default),
            x => x.Kind == WriteKind.RunCreated);
        var duplicate = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(receipt.RunId, (await duplicate.Content.ReadFromJsonAsync<StartReceipt>())!.RunId);
        var conflicting = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs",
            request with { ContextJson = StartRunContextJson.Create() });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
    }

    [Fact]
    public async Task StartRequiresExplicitTokenAndCannotImpersonateByBody()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        using var anonymous = new HttpClient(fixture.Host.Server.CreateHandler())
        { BaseAddress = fixture.Client.BaseAddress };
        var request = new StartPublicRequest("anonymous", StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var result = await anonymous.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(0, fixture.Plc.StartCommands);
    }

    [Fact]
    public async Task AlgorithmAdapterReadinessIsNotAStartGateAndPreHandoffQueriesStayLimited()
    {
        var unavailable = new NotIntegratedAlgorithm();
        await using var fixture = await Station01HostFixture.CreateAsync(services =>
        {
            services.RemoveAll<IAlgorithmPort>();
            services.AddSingleton<IAlgorithmPort>(unavailable);
        });
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var until = DateTimeOffset.UtcNow.AddSeconds(8);
        RunApiSnapshot? snapshot = null;
        while (DateTimeOffset.UtcNow < until)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
            if (snapshot?.State == RunState.Blocked && snapshot.ErrorCode is not null) break;
            await Task.Delay(20);
        }
        Assert.Equal(RunState.Blocked, snapshot?.State);
        Assert.Contains(RunState.WaitingClamp.ToString(), snapshot!.Events);
        Assert.Contains("FCodeNotUniqueAndParsed", snapshot.ErrorCode, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Plc.StartCommands);
        Assert.Equal(0, unavailable.CallCount(AlgorithmRole.Height));
        Assert.Equal(HttpStatusCode.Conflict,
            (await fixture.Client.GetAsync($"/api/v1/station01/runs/{receipt.RunId}/handoff")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.Client.GetAsync($"/api/v1/station01/media/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task InvalidBoundSimulationReturnsStructuredChineseErrorWithoutDeviceAction()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-controlled-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("StartRequestInvalid", body);
        Assert.Contains("未下发设备动作", body);
        Assert.Contains("RejectedBeforeRunAdmission", body);
        Assert.Equal(0, fixture.Plc.StartCommands);
    }

    [Fact]
    public async Task MissingVersionedStartContextIsRejectedBeforeRunAdmissionOrDeviceAction()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), "{}",
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("StartRequestInvalid", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, fixture.Plc.StartCommands);
        Assert.Equal(0, fixture.Plc.MoveCommands);
        Assert.Equal(0, await RecipeBindingTestSupport.PersistedRunCount(fixture));
    }

    [Fact]
    public async Task ValidStartContextFreezesCallerIdentityOnTheRunSnapshot()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var trayId = Guid.NewGuid();
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create("S1", trayId, ["P01", "P02"]),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));

        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);

        Assert.NotNull(snapshot?.Identity);
        Assert.Equal(receipt.RunId, snapshot.Identity.RunId);
        Assert.Equal(trayId, snapshot.Identity.TrayId);
        Assert.Equal("S1", snapshot.Identity.ScenarioId);
        Assert.Equal(["P01", "P02"], snapshot.Identity.OccupiedSlots);
        Assert.Equal("Test", snapshot.Identity.Purpose.ToString());
    }

    private sealed class NotIntegratedAlgorithm : IAlgorithmPort
    {
        public int CallCount(AlgorithmRole role) => 0;
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
            Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken) =>
            throw new AlgorithmNotDispatchedException(request.CallId, "NotIntegrated");
    }
}
