using System.Diagnostics;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Gaode.Plc.Protocol;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

[Collection("CommunicationTcp")]
public sealed class SiteOperationHandshakeTests
{
    private sealed class Evidence : IAsyncDisposable
    {
        internal readonly string Root = Path.Combine(Environment.GetEnvironmentVariable("GAODE_SITE_OPERATIONS_EVIDENCE")
            ?? Path.GetTempPath(), "site-operations-" + Guid.NewGuid().ToString("N"));
        private readonly TraceWriter writer;
        private readonly IDisposable logging;
        internal CommunicationEvidenceRecorder Recorder { get; }
        internal Evidence()
        {
            Directory.CreateDirectory(Root);
            var options = new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite($"Data Source={Path.Combine(Root, "run.db")};Pooling=False").Options;
            var storeId = Guid.NewGuid();
            using (var db = new Station01DbContext(options))
            {
                db.Database.Migrate();
                db.Manifests.Add(new() { StoreId = storeId, SchemaVersion = "s01-store/2", Profile = "Test",
                    PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
                db.SaveChanges();
            }
            writer = new(options, TimeProvider.System, 32);
            Recorder = new(writer, storeId, TimeProvider.System, 2000);
            logging = new Gaode.Infrastructure.Diagnostics.RuntimeDiagnosticLogging(
                new ControlledCommissioningTests.FileLogger(Path.Combine(Root, "runtime.log")));
        }
        internal void Save(SiteProtocolTcpFixture fixture, string result) => File.WriteAllText(Path.Combine(Root, "wire-audit.json"),
            JsonSerializer.Serialize(new { environment = "OFFLINE_LOOPBACK", result, fixture.ResetEdges, fixture.StartEdges,
                fixture.MotionEdges, writes = fixture.Writes.Select(w => new { w.Offset, w.Words, w.Accepted }),
                resetPreconditions = fixture.ResetPreconditions.Select(p => new { p.PcReady, p.SoftStop, p.Readbacks }),
                startClears = fixture.StartClears.Select(s => new { s.ZLow, s.ZHigh, s.ZRequest, s.ZFeedback }) },
                new JsonSerializerOptions { WriteIndented = true }));
        public async ValueTask DisposeAsync() { await writer.DisposeAsync(); logging.Dispose(); }
    }
    private static LatestProtocolPlcDevice Device(SiteProtocolTcpFixture plc, Evidence evidence) => new(new PlcRuntimeOptions
    {
        Provider = "Real", Purpose = RuntimePurposes.RealDeviceCommissioning,
        Host = "127.0.0.1", Port = plc.Port, IoTimeoutMs = 1000, HeartbeatTimeoutMs = 3000,
        Definition = ConfirmedMemoryLayout.Load().CreateDefinition(SiteProtocolAdaptationTests.Profile()),
        PositionBasis = new("OFFLINE_ONLY", "mm", RuntimePurposes.RealDeviceCommissioning, "Offline:confirmed-sequence-fixture"),
        SiteOperations = new("plc-site-operations/1", "User:2026-10-08-ready-edge-start-stop-zero")
    }, .01, recorder: evidence.Recorder);
    private static void Zero(SiteProtocolTcpFixture plc)
    { foreach (var mb in new[] { 6064, 6076, 6084, 6088, 6092 }) { plc.SetWord(mb, 0); plc.SetWord(mb + 2, 0); } }
    private static PortEnvelope Envelope()
    {
        var tick = Stopwatch.GetTimestamp();
        return new(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "offline-site", "1", RuntimePurposes.RealDeviceCommissioning,
            tick, tick + 10 * Stopwatch.Frequency, "Stopwatch");
    }
    private static async Task Until(Func<bool> predicate, CancellationToken token)
    { while (!predicate()) await Task.Delay(10, token); }
    private static async Task<DeviceEvent> Start(LatestProtocolPlcDevice device, CancellationToken token)
    {
        var completion = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await device.RequestStartAsync(Envelope(), Guid.NewGuid(), Guid.NewGuid(), e => completion.TrySetResult(e), token);
        return await completion.Task.WaitAsync(token);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetWaitsForThisReadyFallingAndRisingEdgeBeforeClearingRequest(bool initiallySoftStopped)
    {
        using var deadline = new CancellationTokenSource(15000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true }; Zero(plc);
        await using var device = Device(plc, evidence);
        await device.StartAsync(deadline.Token);
        plc.SetByte(2006, 0); plc.SetByte(2008, initiallySoftStopped ? (byte)1 : (byte)0);
        var reset = device.ResetAsync(deadline.Token);
        await Until(() => plc.Byte(2009) == 1, deadline.Token);
        var preconditions = Assert.Single(plc.ResetPreconditions);
        Assert.Equal(1, preconditions.PcReady); Assert.Equal(0, preconditions.SoftStop);
        Assert.True(preconditions.Readbacks > 0);
        Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        await Task.Delay(300, deadline.Token); // PLC holds old Ready=1; must not count as this reset.
        Assert.False(reset.IsCompleted); Assert.Equal(1, plc.Byte(2009));
        plc.SetByte(6015, 0); await Task.Delay(300, deadline.Token);
        Assert.False(reset.IsCompleted); Assert.Equal(1, plc.Byte(2009));
        plc.SetByte(6015, 1); await reset;
        Assert.Equal(0, plc.Byte(2009)); Assert.Equal(1, plc.ResetEdges);
        var log = File.ReadAllText(Path.Combine(evidence.Root, "runtime.log"));
        Assert.Contains("ResetNotReadyObserved", log); Assert.Contains("ResetCompleted", log);
        Assert.Contains("ResetPreconditionsConfirmed", log);
        Assert.Contains("ResetRequestObserved", log); Assert.Contains("ResetReadyObserved", log);
        Assert.Contains("ResetVerificationObserved", log); Assert.Contains("ResetRequestClearWriteResponded", log);
        evidence.Save(plc, "Reset edge and persistent log verified");
    }
    [Fact]
    public async Task ResetPreconditionReadbackFailureNeverSendsResetOrStart()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        plc.SetByte(2008, 1); plc.KeepSoftStopAsserted = true;
        var error = await Assert.ThrowsAsync<IOException>(() => device.ResetAsync(deadline.Token));
        Assert.Contains("ResetPreconditionsNotConfirmed", error.Message);
        Assert.Equal(0, plc.ResetEdges); Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        evidence.Save(plc, "Unconfirmed soft-stop readback blocks reset/start/motion despite accepted writes");
    }
    [Fact]
    public async Task PreviousResetRequestIsNotClearedOrReplayed()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        plc.SetByte(2009, 1); plc.SetByte(2008, 1);
        var error = await Assert.ThrowsAsync<IOException>(() => device.ResetAsync(deadline.Token));
        Assert.Equal("PreviousResetRequestNotReleased", error.Message);
        Assert.Equal(1, plc.Byte(2009)); Assert.Equal(1, plc.Byte(2008));
        Assert.Equal(0, plc.ResetEdges); Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        evidence.Save(plc, "Pending old reset is not cleared or retriggered");
    }
    [Fact]
    public async Task ResetOldReadyOneTimesOutWithoutClearingOrReplaying()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        using var resetDeadline = new CancellationTokenSource(700);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => device.ResetAsync(resetDeadline.Token));
        Assert.Equal(1, plc.Byte(2009)); Assert.Equal(1, plc.ResetEdges);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Start(device, deadline.Token));
        Assert.Equal(0, plc.StartEdges);
        evidence.Save(plc, "Old Ready is not completion; timeout blocks start and does not replay");
    }
    [Fact]
    public async Task StartClearsAfterFullActualXYAndZClosureAndSameCoordinateReuseStillWorks()
    {
        using var deadline = new CancellationTokenSource(20000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        Assert.Equal(DeviceEventKind.Accepted, (await Start(device, deadline.Token)).Kind);
        Assert.Equal(1, plc.Byte(2007)); Assert.Equal(1, plc.StartEdges);
        var target = new FixedPoint("offline", "1", 1, 2, "mm", "OFFLINE_ONLY", 3);
        for (var round = 0; round < 2; round++)
        {
            var finished = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            await device.RequestMoveAsync(new(Envelope(), Guid.NewGuid(), target, Guid.NewGuid(), "offline", "Detection"),
                e => { if (e.Kind is DeviceEventKind.Completed or DeviceEventKind.Failed) finished.TrySetResult(e); }, deadline.Token);
            var result = await finished.Task.WaitAsync(deadline.Token);
            Assert.True(result.Kind == DeviceEventKind.Completed, result.ErrorCode ?? device.Failure);
            var position = Assert.Single(result.Evidence!.Positions);
            var tick = Stopwatch.GetTimestamp(); var utc = DateTimeOffset.UtcNow;
            var window = new ActionWindow(tick, tick + 5 * Stopwatch.Frequency, "Stopwatch", utc, utc.AddSeconds(5));
            var capture = await device.OpenCaptureWindowAsync(new(position.Correlation, CaptureRole.Detection, target, position, window), deadline.Token);
            await device.FinishCaptureWindowAsync(capture, new(position.Correlation.RunId, position.Correlation.OperationId, [Guid.NewGuid()], true), window, deadline.Token);
        }
        var clear = Assert.Single(plc.StartClears);
        Assert.Equal(0, clear.ZLow); Assert.Equal(0x4040, clear.ZHigh); // independent CDAB 3.0
        Assert.Equal(0, clear.ZRequest); Assert.Equal(0, clear.ZFeedback);
        Assert.Equal(0, plc.Byte(2007)); Assert.Equal(1, plc.StartEdges);
        Assert.Equal(3, plc.MotionEdges); // X, Y, Z only once; shared-register preservation is not another edge.
        evidence.Save(plc, "Full XYZ closure before startup clear; second same-target action reused");
    }
    [Fact]
    public async Task StartupAwayFromSafeZeroDoesNotSendStartOrMotion()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true }; // default XYZ=1.25
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        var result = await Start(device, deadline.Token);
        Assert.Equal(DeviceEventKind.Failed, result.Kind); Assert.Contains("StartupSafeZeroUnconfirmed", result.ErrorCode);
        Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.ResetEdges);
        evidence.Save(plc, "Nonzero startup position rejected without start or automatic homing");
    }
    [Fact]
    public async Task SoftStopRequestsStopWithoutResetOrHomingAndHoldsUnknown()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true };
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        await device.RequestStopAsync(Envelope(), _ => { }, deadline.Token);
        await Until(() => plc.Byte(2008) == 1 && device.Failure is not null, deadline.Token);
        Assert.Equal(0, plc.ResetEdges); Assert.Equal(0, plc.StartEdges);
        Assert.Equal(0x3fa0, plc.Word(6066)); // remains 1.25; no auto return.
        Assert.Contains("PhysicalStopUnconfirmed", device.Failure);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Start(device, deadline.Token));
        evidence.Save(plc, "Soft stop dispatched; no automatic reset/homing/resume; physical stop not fabricated");
    }
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(256)]
    public async Task ConfirmedPolicyDoesNotOverrideCurtainDoorOrUnknownAlarm(int alarm)
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true }; Zero(plc);
        plc.SetWord(6056, (ushort)alarm); plc.SetWord(6058, 0);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        Assert.Equal(SafetyAssessment.ExplicitUnsafe, device.Observe().SafetyAssessment);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Start(device, deadline.Token));
        Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        evidence.Save(plc, "Confirmed semantics never override active or unknown alarms");
    }
}
