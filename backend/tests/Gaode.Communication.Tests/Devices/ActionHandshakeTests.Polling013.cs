using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class ActionHandshakeTests
{
    [Theory]
    [InlineData("axis-150ms")]
    [InlineData("flip-80ms")]
    [InlineData("putback-80ms")]
    public async Task FirstIntermediateStateIsRequiredAndFiniteFastSamplingObservesIt(string caseId)
    {
        // Two finite component cases: actual local-fast observation, then an actual
        // request arriving after a 200 ms blind interval. Simulator durations never change.
        foreach (var miss in new[] { false, true })
        {
            using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(watchdog.Token);
            await using var probe = new PlcPolling013WireProbe(plc.Port); probe.Start();
            await using var device = plc.Device(communicationPort: probe.Port, posePrograms: ComponentPrograms);
            var captureStartedUtc = DateTimeOffset.UtcNow;
            var captureStartedTick = Stopwatch.GetTimestamp();
            try
            {
            await StartAndPrepareAsync(device, watchdog.Token);
            Func<Task> execute;
            int offset; int command; int commandValue;
            if (caseId == "axis-150ms")
            {
                offset = 0x7F; command = 35; commandValue = 1;
                execute = async () => { await MoveAsync(device, "3D", new("fast-axis", "1", 12, 24, "mm", "SIM_MACHINE", 0), watchdog.Token); };
            }
            else
            {
                var (_, flip) = await PreparedFlip(device, watchdog.Token);
                if (caseId == "flip-80ms")
                {
                    offset = 21; command = 96; commandValue = 1;
                    execute = async () => { await device.FlipAsync(flip, watchdog.Token); };
                }
                else
                {
                    await device.FlipAsync(flip, watchdog.Token);
                    var placed = await MoveTransition(device, flip.Correlation.RunId, "FlipPutBack",
                        new("put", "test/1", 14, 28, "mm", "SIM_MACHINE", 45), null, watchdog.Token);
                    var put = new PutBackRequest(flip.Correlation with { ActionId = Guid.NewGuid(), OperationId = Guid.NewGuid() },
                        flip.TransitionId, Assert.Single(placed.Positions), Window(ProtocolTcpFixture.Envelope()), Guid.NewGuid());
                    offset = 96; command = 96; commandValue = 2;
                    execute = async () => { await device.PutBackAsync(put, watchdog.Token); };
                }
            }
            var before = plc.Store.GetWriteAudit().Last().Sequence;
            var start = Stopwatch.GetTimestamp(); var delayed = 0;
            probe.DelayBeforeForward = (function, requestedOffset) => miss && function == 3 && requestedOffset == offset &&
                plc.Store.GetWriteAudit().Any(w => w.Sequence > before && w.DocumentNumber == command && w.Value == commandValue) &&
                Interlocked.Exchange(ref delayed, 1) == 0 ? 200 : 0;
            var error = await Record.ExceptionAsync(execute);
            var responses = probe.Exchanges.Where(e => e.Received >= start && e.Function == 3 && e.Offset == offset).ToArray();
            if (miss)
            {
                Assert.NotNull(error); Assert.Equal(1, delayed);
                Assert.Equal(MotionAvailability.HeldUnknown, device.Observe().MotionAvailability);
                Assert.DoesNotContain(responses, e => PlcPolling013AcquisitionTests.Word(e, offset) == (caseId == "axis-150ms" ? 0 : 1));
                Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before), w =>
                    w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 96 && w.Value == (caseId == "flip-80ms" ? 2 : 0));
            }
            else
            {
                Assert.Null(error);
                var intermediate = responses.First(e => PlcPolling013AcquisitionTests.Word(e, offset) == (caseId == "axis-150ms" ? 0 : 1));
                if (caseId == "axis-150ms") Assert.Equal((ushort)0, PlcPolling013AcquisitionTests.Word(intermediate, 0x80));
                var completed = responses.First(e => e.Received > intermediate.Received &&
                    PlcPolling013AcquisitionTests.Word(e, offset) == (caseId == "axis-150ms" ? 1 : 2));
                // After the first intermediate observation, ordinary 200 ms service owns this field.
                Assert.True(Stopwatch.GetElapsedTime(intermediate.Received, completed.Received).TotalMilliseconds >= 175);
            }
            }
            finally
            {
                probe.Save("I-FAST-" + caseId + (miss ? "-miss" : "-observed"));
                SaveComponentDiagnostic(plc, device, "I-FAST-" + caseId + (miss ? "-miss" : "-observed"),
                    captureStartedUtc, captureStartedTick);
            }
        }
    }
    private static void SaveComponentDiagnostic(ProtocolTcpFixture plc, LatestProtocolPlcDevice device,
        string caseId, DateTimeOffset startedUtc, long startedTick, object? wire = null)
    {
        if (Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") is not { } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, caseId + "-diagnostic-" + Guid.NewGuid().ToString("N") + ".json"),
            System.Text.Json.JsonSerializer.Serialize(new { caseId, startedUtc, startedTick,
                endedUtc = DateTimeOffset.UtcNow, endedTick = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency,
                processId = Environment.ProcessId, device.Failure, observation = device.Observe(),
                deviceActions = plc.Engine.GetActionAudit(), writes = plc.Store.GetWriteAudit(),
                logs = plc.DeviceDiagnostics, evidenceStore = plc.EvidenceStorePath, wire }));
    }

}
