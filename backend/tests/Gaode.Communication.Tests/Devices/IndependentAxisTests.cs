using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class IndependentAxisTests
{
    private sealed record Axis(string Purpose, int Target, int Start, int Confirmed, int Actual);
    private static Axis[] Oracle()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ProtocolOracle", "confirmed-011.json")));
        return document.RootElement.GetProperty("axes").Deserialize<Axis[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    private static ushort Offset(int number) => checked((ushort)(number - 1));
    private static ushort[] Words(float value)
    {
        var bits = BitConverter.SingleToUInt32Bits(value);
        return [(ushort)(bits >> 16), (ushort)bits];
    }
    private static async Task<float> Actual(ModbusTcpClient client, int address, CancellationToken token)
    {
        var words = await client.ReadRegistersAsync(Offset(address), 2, token);
        return BitConverter.UInt32BitsToSingle((uint)words[0] << 16 | words[1]);
    }

    [Fact]
    public async Task FiveAxesAdvanceIndependentlyAndTargetWritesDoNotBecomeActualPositions()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var token = deadline.Token;
        await using var fixture = new ProtocolTcpFixture(motionDurationMs: 150);
        await fixture.StartAsync(token);
        await using var device = fixture.Device();
        await device.StartAsync(token);
        await device.ResetAsync(token);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation &&
            device.Observe().Readiness == DeviceReadiness.Ready, token);
        await using var client = fixture.Client();
        var axes = Oracle();
        var expected = new float[5];
        for (var i = 0; i < axes.Length; i++)
        {
            var axis = axes[i]; var target = 11.5f + i * 13;
            await client.WriteRegistersAsync(Offset(axis.Target), Words(target), token);
            Assert.Equal(expected[i], await Actual(client, axis.Actual, token));
            await client.WriteCoilAsync(Offset(axis.Start), true, token);
            var sawMoving = false; var observedInUse = false;
            while (true)
            {
                Assert.False(fixture.Engine.ExecuteTask?.IsFaulted, fixture.Engine.ExecuteTask?.Exception?.ToString());
                var state = (await client.ReadRegistersAsync(Offset(axis.Confirmed), 1, token))[0];
                if (state == 0) sawMoving = true;
                observedInUse |= device.Observe().MotionAvailability == MotionAvailability.InUse;
                if (state == 1 && sawMoving) break;
                Assert.InRange(state, (ushort)0, (ushort)1);
                await Task.Delay(5, token);
            }
            Assert.True(observedInUse, "Actual independent movement must reach the semantic status query");
            expected[i] = target;
            for (var n = 0; n < axes.Length; n++) Assert.Equal(expected[n], await Actual(client, axes[n].Actual, token));
            await client.WriteCoilAsync(Offset(axis.Start), false, token);
        }
        var completed = fixture.Engine.GetActionAudit().Actions.Where(a => a.Kind == "AxisMove" && a.Phase == "completed").ToArray();
        Assert.Equal(5, completed.Length);
        Assert.Equal(axes.Select(a => a.Purpose), completed.Select(a => a.AxisRole));
        Assert.All(completed, a => Assert.Equal(2, a.WriteSequenceRefs.Count));
    }

    [Fact]
    public async Task ScanAndDetectionUseTheirOwnZAndCaptureReleaseWritesNoLegacyAcknowledgement()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(18));
        var token = deadline.Token;
        await using var fixture = new ProtocolTcpFixture(motionDurationMs: 150);
        await fixture.StartAsync(token);
        await using var device = fixture.Device();
        await ActionHandshakeTests.StartAndPrepareAsync(device, token);
        foreach (var (role, z, purpose) in new[] { ("3D", 90d, "XY"), ("E", 32d, "ScanZ"), ("Detection", 64d, "DetectionZ") })
        {
            var before = fixture.Store.GetWriteAudit().Last().Sequence;
            var target = new FixedPoint(role, "test/1", 12, 24, "mm", "SIM_MACHINE", z);
            var reached = await ActionHandshakeTests.MoveAsync(device, role, target, token);
            var position = Assert.Single(reached.Positions);
            Assert.Equal(purpose, position.Actual.AxisPurpose);
            if (purpose == "XY") Assert.Null(position.Actual.ActualZ); else Assert.Equal(z, position.Actual.ActualZ);
            var session = await device.OpenCaptureWindowAsync(new(reached.Correlation,
                role == "3D" ? CaptureRole.ThreeD : role == "E" ? CaptureRole.E : CaptureRole.Detection,
                target, position, ActionHandshakeTests.Window(ProtocolTcpFixture.Envelope())), token);
            using var saved = await CaptureWorkFixture.CreateAsync(session);
            var result = await device.FinishCaptureWindowAsync(session, saved.Work,
                ActionHandshakeTests.Window(ProtocolTcpFixture.Envelope()), token);
            Assert.True(result.State == AcquisitionState.Released, JsonSerializer.Serialize(result));
            Assert.DoesNotContain(fixture.Store.GetWriteAudit().Where(w => w.Sequence > before), w =>
                w.Area == PlcArea.HoldingRegister && w.DocumentNumber is 1 or 82 or 83);
        }
        Assert.Equal(64, device.Observe().AxisPositions!.DetectionZ);
        Assert.Equal(32, device.Observe().AxisPositions!.ScanZ);
        Assert.Equal(0, device.Observe().AxisPositions!.GrabZ);
    }

    [Fact]
    public async Task CancellationAtXyAcceptanceCannotDispatchDependentDetectionZ()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var fixture = new ProtocolTcpFixture(motionDurationMs: 150);
        await fixture.StartAsync(watchdog.Token);
        await using var device = fixture.Device();
        await ActionHandshakeTests.StartAndPrepareAsync(device, watchdog.Token);
        var before = fixture.Store.GetWriteAudit().Last().Sequence;
        using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(watchdog.Token);
        var ended = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new MoveRequest(ProtocolTcpFixture.Envelope(), Guid.NewGuid(),
            new("cancelled-detection", "Test/1", 12, 24, "mm", "SIM_MACHINE", 64),
            Guid.NewGuid(), "component-binding", "Detection");
        await device.RequestMoveAsync(request, value =>
        {
            if (value.Kind == DeviceEventKind.Accepted) actionCancellation.Cancel();
            if (value.Kind is DeviceEventKind.Failed or DeviceEventKind.UnknownHeld or DeviceEventKind.Completed)
                ended.TrySetResult(value);
        }, actionCancellation.Token);
        var terminal = await ended.Task.WaitAsync(watchdog.Token);
        Assert.Equal(request.ActionId, terminal.ActionId);
        Assert.NotEqual(DeviceEventKind.Completed, terminal.Kind);
        Assert.True(actionCancellation.IsCancellationRequested);
        Assert.Equal(MotionAvailability.HeldUnknown, device.Observe().MotionAvailability);
        var z = Oracle().Single(axis => axis.Purpose == "DetectionZ");
        Assert.DoesNotContain(fixture.Store.GetWriteAudit().Where(write => write.Sequence > before), write =>
            write.Area == PlcArea.Coil && write.DocumentNumber == z.Start && write.Value != 0 ||
            write.Area == PlcArea.HoldingRegister && write.DocumentNumber == z.Target);
    }
}
