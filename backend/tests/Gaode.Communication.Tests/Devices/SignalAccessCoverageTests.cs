using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Infrastructure.Devices.Plc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class SignalAccessCoverageTests
{
    [Fact]
    public async Task RuntimeZonePreparationAndStopUseFormalSignalsWithoutInventingStoppedFeedback()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await ActionHandshakeTests.StartAndPrepareAsync(device, watchdog.Token);
        var notifications = new List<Gaode.Application.Ports.DeviceEvent>();
        await device.RequestStopAsync(ProtocolTcpFixture.Envelope(), notifications.Add, watchdog.Token);
        await ProtocolTcpFixture.UntilAsync(() => plc.Store.GetWriteAudit().Any(w =>
            w.Area == Gaode.Plc.Protocol.PlcArea.Coil && w.DocumentNumber == 6 && w.Value == 1 && w.Accepted), watchdog.Token);
        Assert.DoesNotContain(notifications, e => e.Kind is Gaode.Application.Ports.DeviceEventKind.Stopped
            or Gaode.Application.Ports.DeviceEventKind.Completed);
        await AssertRouteAsync(device, "startup-A-reset-stop", 0,
            new Dictionary<string, ushort[][]> {
                ["System_Reset_Cmd"] = [[1], [0]], ["PC_Start_Cmd"] = [[0], [0], [1], [0]],
                ["Zone_Config_Ready"] = [[0], [1]], ["NG_Zone_Count"] = [[15]],
                ["Pending_Zone_Count"] = [[15]], ["Soft_Stop_Cmd"] = [[1]] }, watchdog.Token);
    }

    [Fact]
    public async Task FormalInitializationReadsEveryDeclaredFieldAndProbeRejectsFalseReceipts()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await ActionHandshakeTests.StartAndPrepareAsync(device, watchdog.Token);
        var exchanges = device.BusinessExchanges.Concat(device.HeartbeatExchanges).ToArray();
        // Journal sequence belongs to a transport channel and continues across Reset's
        // new TCP connection. Verify continuity without inventing a per-connection restart.
        Assert.All(exchanges.GroupBy(e => e.Channel), group =>
            Assert.Equal(Enumerable.Range(1, group.Count()).Select(i => (long)i), group.Select(e => e.Sequence).Order()));
        var workspace = Workspace();
        var runId = Guid.NewGuid().ToString("D");
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("WireEvidenceRootRequired");
        var directory = Path.Combine(evidenceRoot, "WIRE-ACCESS-" + runId);
        Directory.CreateDirectory(directory);
        var payload = JsonSerializer.SerializeToNode(new { schemaVersion = "009-wire-evidence/1",
            runId, caseId = "WIRE-ACCESS/formal-initialization", gap = false, exchanges,
            store = plc.EvidenceStorePath, scope = "FormalAdapterRealTcpComponent" },
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(0, await ProbeAsync(payload, "all-reads", true));

        // Literal independent expected signal/address/value, not values obtained from the DUT table.
        var ready = exchanges.First(e => e.Request.EndsWith("050002FF00", StringComparison.Ordinal) && e.Response is not null);
        var expected = Path.Combine(directory, "literal-expectation.json");
        await File.WriteAllTextAsync(expected,
            "{\"basis\":\"confirmed protocol 2.1 PC_System_Ready true at Coil 0003\",\"writeValues\":{\"PC_System_Ready\":[[1]]}}", watchdog.Token);
        var one = payload.DeepClone();
        one["caseId"] = "WIRE-ACCESS/literal-ready";
        one["exchanges"] = JsonSerializer.SerializeToNode(new[] { ready }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(0, await ProbeAsync(one, "literal-ready", false, expected));
        var falsePair = one.DeepClone();
        var row = falsePair["exchanges"]![0]!;
        row["request"] = ready.Request[..^4] + "0000";
        row["response"] = ready.Response![..^4] + "0000";
        Assert.Equal(1, await ProbeAsync(falsePair, "both-ends-wrong", false, expected));
        using (var result = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "both-ends-wrong.result.json"), watchdog.Token)))
            Assert.Equal("IndependentWriteSequenceMismatch:PC_System_Ready", result.RootElement.GetProperty("reason").GetString());
        var missingReceipt = one.DeepClone();
        missingReceipt["exchanges"]![0]!["response"] = null;
        missingReceipt["exchanges"]![0]!["error"] = null;
        Assert.Equal(1, await ProbeAsync(missingReceipt, "missing-receipt", false));

        async Task<int> ProbeAsync(JsonNode value, string name, bool allReads, string? expectation = null)
        {
            var input = Path.Combine(directory, name + ".json");
            await File.WriteAllTextAsync(input, value.ToJsonString(), watchdog.Token);
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("GAODE_TEST_PYTHON") ?? "python")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = workspace };
            foreach (var arg in new[] { "scripts/communication/check-009-wire-evidence.py", "--evidence", input,
                "--oracle", "backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json",
                "--output", Path.Combine(directory, name + ".result.json") }) start.ArgumentList.Add(arg);
            if (allReads) start.ArgumentList.Add("--require-all-reads");
            if (expectation is not null) { start.ArgumentList.Add("--expectation"); start.ArgumentList.Add(expectation); }
            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync(watchdog.Token);
            var stderr = process.StandardError.ReadToEndAsync(watchdog.Token);
            await process.WaitForExitAsync(watchdog.Token);
            await File.WriteAllTextAsync(Path.Combine(directory, name + ".stdout.txt"), await stdout, watchdog.Token);
            Assert.True(string.IsNullOrWhiteSpace(await stderr), await stderr);
            return process.ExitCode;
        }
    }
    internal static async Task AssertRouteAsync(LatestProtocolPlcDevice device, string route, long afterSequence,
        IReadOnlyDictionary<string, ushort[][]> expectedWrites, CancellationToken token)
    {
        // The short monitor window is not an action journal. Use the same bounded
        // evidence journal as the production recorder and explicitly reject any gap.
        var journal = device.BusinessEvidenceSince(afterSequence);
        Assert.False(journal.Gap);
        var exchanges = journal.Exchanges.ToArray();
        Assert.NotEmpty(exchanges);
        Assert.Equal(afterSequence + 1, exchanges[0].Sequence);
        Assert.Equal(Enumerable.Range(1, exchanges.Length).Select(i => afterSequence + i), exchanges.Select(e => e.Sequence));
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("WireEvidenceRootRequired");
        var runId = Guid.NewGuid().ToString("D");
        var directory = Path.Combine(root, "WIRE-ROUTE-" + route + "-" + runId);
        Directory.CreateDirectory(directory);
        var input = Path.Combine(directory, "exchanges.json");
        var expectation = Path.Combine(directory, "expectation.json");
        await File.WriteAllTextAsync(input, JsonSerializer.Serialize(new {
            schemaVersion = "009-wire-evidence/1", runId, caseId = "WIRE-ROUTE/" + route,
            gap = false, exchanges, scope = "FormalAdapterRealTcpComponent" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), token);
        await File.WriteAllTextAsync(expectation, JsonSerializer.Serialize(new {
            basis = "confirmed-20260925.json: manually transcribed protocol; literal expected values in communication test",
            writeValues = expectedWrites }), token);
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("GAODE_TEST_PYTHON") ?? "python") {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = Workspace() };
        foreach (var arg in new[] { "scripts/communication/check-009-wire-evidence.py", "--evidence", input,
            "--oracle", "backend/tests/Gaode.Communication.Tests/ProtocolOracle/confirmed-20260925.json",
            "--expectation", expectation, "--output", Path.Combine(directory, "result.json") }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        await File.WriteAllTextAsync(Path.Combine(directory, "stdout.txt"), await stdout, token);
        Assert.True(process.ExitCode == 0, await stdout + await stderr);
    }
    private static string Workspace()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("WorkspaceNotFound");
    }
}
