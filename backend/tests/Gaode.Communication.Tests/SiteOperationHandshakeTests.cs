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
                fixture.ResetReadyZeroReads, fixture.ResetReadyOneReads,
                fixture.MotionEdges, resetActionRequests = fixture.ResetActionRequests.ToArray(), writes = fixture.Writes.Select(w => new { w.Offset, w.Words, w.Accepted }),
                resetPreconditions = fixture.ResetPreconditions.Select(p => new { p.PcReady, p.SoftStop, p.Readbacks }),
                startClears = fixture.StartClears.Select(s => new { s.ZLow, s.ZHigh, s.ZRequest, s.ZFeedback }) },
                new JsonSerializerOptions { WriteIndented = true }));
        public async ValueTask DisposeAsync() { await writer.DisposeAsync(); logging.Dispose(); }
    }
    private static LatestProtocolPlcDevice Device(SiteProtocolTcpFixture plc, Evidence evidence, double? safeZeroToleranceMm = null) => new(new PlcRuntimeOptions
    {
        Provider = "Real", Purpose = RuntimePurposes.RealDeviceCommissioning,
        Host = "127.0.0.1", Port = plc.Port, IoTimeoutMs = 1000, HeartbeatTimeoutMs = 3000,
        Definition = ConfirmedMemoryLayout.Load().CreateDefinition(SiteProtocolAdaptationTests.Profile()),
        PositionBasis = new("OFFLINE_ONLY", "mm", RuntimePurposes.RealDeviceCommissioning, "Offline:confirmed-sequence-fixture"),
        SiteOperations = new("plc-site-operations/1", "User:2026-10-09-R5-ready-one-after-reset-request")
        { SafeZeroToleranceMm = safeZeroToleranceMm, RestoresWorkpieceAndMechanisms = safeZeroToleranceMm is not null },
        RotationBasis = new(.01, RuntimePurposes.RealDeviceCommissioning, "OFFLINE-ROTATION")
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
    private static string ReadLiveLog(string root)
    {
        using var stream = new FileStream(Path.Combine(root, "runtime.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    [Theory]
    [InlineData(.15085936, true)]
    [InlineData(-.2, true)]
    [InlineData(.2, true)]
    [InlineData(.21, false)]
    public async Task SafeZeroToleranceIsIndependentOfMotionTolerance(double y, bool accepted)
    {
        using var deadline=new CancellationTokenSource(10000);
        await using var evidence=new Evidence();
        await using var plc=new SiteProtocolTcpFixture {ExerciseConfirmedOperations=true,ReadyOnReset=1};Zero(plc);
        var raw=BitConverter.SingleToInt32Bits((float)y);plc.SetWord(6076,(ushort)(raw&0xffff));plc.SetWord(6078,(ushort)((uint)raw>>16));
        await using var device=Device(plc,evidence,.2);await device.StartAsync(deadline.Token);
        Assert.Equal(.01,device.PositionTolerance);
        if (accepted)
        {
            await device.ResetAsync(deadline.Token);
            Assert.Equal(InitialReadiness.Ready,(await device.ReadInitialStateAsync(deadline.Token)).Readiness);
            Assert.Equal(DeviceEventKind.Accepted,(await Start(device,deadline.Token)).Kind);
            Assert.Contains("safeZeroToleranceMm",ReadLiveLog(evidence.Root));
        }
        else
        {
            var error=await Assert.ThrowsAsync<IOException>(()=>device.ResetAsync(deadline.Token));
            Assert.Equal("StartupSafeZeroUnconfirmed:MachineCurrentPosY",error.Message);
            Assert.Equal(0,plc.StartEdges);
        }
        Assert.Equal(0,plc.Byte(2009));Assert.Equal(0,plc.MotionEdges);
        evidence.Save(plc,"Safe zero tolerance independently applies to reset, recovery and start; motion tolerance unchanged");
    }
    [Fact]
    public async Task ReleasedRequestWithMovingFeedbackIsNotReportedIdle()
    {
        using var deadline=new CancellationTokenSource(10000);
        await using var evidence=new Evidence();
        await using var plc=new SiteProtocolTcpFixture {ExerciseConfirmedOperations=true,ReadyOnReset=1};Zero(plc);
        await using var device=Device(plc,evidence);await device.StartAsync(deadline.Token);await device.ResetAsync(deadline.Token);
        plc.SetWord(6040,0);
        await Until(()=>device.Observe().MotionAvailability==MotionAvailability.InUse,deadline.Token);
        Assert.Equal(0,plc.Byte(2001));
        evidence.Save(plc,"Request zero plus feedback moving is never interpreted as stationary");
    }
    [Fact]
    public async Task ResidualPcRequestsAreClearedAndReadBeforeSystemResetEdge()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 1 }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        foreach (var mb in new[] { 2000, 2001, 2002, 2003, 2004, 2005, 2007 }) plc.SetByte(mb, 1);
        plc.SetWord(2014, 2); plc.SetWord(2016, 1); plc.SetByte(2008, 1);
        await device.ResetAsync(deadline.Token);
        Assert.All(Assert.Single(plc.ResetActionRequests), value => Assert.Equal(0, value));
        Assert.Equal(1, plc.ResetEdges); Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        Assert.All(plc.Writes, w => Assert.InRange(w.Offset, 1000, 1027));
        var log = ReadLiveLog(evidence.Root);
        Assert.Contains("ResetPcRequestsReadback", log);
        Assert.True(log.IndexOf("ResetPcRequestsConfirmed", StringComparison.Ordinal) < log.IndexOf("ResetRequestWriteStarted", StringComparison.Ordinal));
        evidence.Save(plc, "All nine PC action requests zero and freshly read before system reset; PLC feedback never written");
    }
    [Fact]
    public async Task AcknowledgedClearWithoutZeroReadbackBlocksSystemReset()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 1 }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        plc.SetByte(2001, 1); plc.KeepXRequestAsserted = true;
        var error = await Assert.ThrowsAsync<IOException>(() => device.ResetAsync(deadline.Token));
        Assert.Equal("ResetPcRequestsNotCleared:XMoveStart", error.Message);
        Assert.Equal(0, plc.ResetEdges); Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        Assert.Equal(0, plc.Byte(2009)); Assert.Equal(1, plc.Byte(2001));
        Assert.Contains("ResetPcRequestsFailed", ReadLiveLog(evidence.Root));
        evidence.Save(plc, "Acknowledged zero write with actual residual X request blocks before reset dispatch");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetWaitsForFreshReadyAfterRequestInsteadOfUsingEarlierReady(bool initiallySoftStopped)
    {
        using var deadline = new CancellationTokenSource(15000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 0 }; Zero(plc);
        await using var device = Device(plc, evidence);
        await device.StartAsync(deadline.Token);
        plc.SetByte(2006, 0); plc.SetByte(2008, initiallySoftStopped ? (byte)1 : (byte)0);
        var reset = device.ResetAsync(deadline.Token);
        await Until(() => plc.Byte(2009) == 1, deadline.Token);
        var preconditions = Assert.Single(plc.ResetPreconditions);
        Assert.Equal(1, preconditions.PcReady); Assert.Equal(0, preconditions.SoftStop);
        Assert.True(preconditions.Readbacks > 0);
        Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        // Ready was 1 before dispatch; the PLC now reports 0, so cached Ready cannot complete it.
        await Until(() => plc.ResetReadyZeroReads > 0, deadline.Token);
        await Task.Delay(100, deadline.Token);
        Assert.False(reset.IsCompleted); Assert.Equal(1, plc.Byte(2009));
        plc.SetByte(6015, 1); await reset;
        Assert.Equal(0, plc.Byte(2009)); Assert.Equal(1, plc.ResetEdges);
        Assert.True(plc.ResetReadyOneReads > 0);
        var log = File.ReadAllText(Path.Combine(evidence.Root, "runtime.log"));
        Assert.Contains("ResetNotReadyObserved", log); Assert.Contains("ResetCompleted", log);
        Assert.Contains("ResetPreconditionsConfirmed", log);
        Assert.Contains("ResetRequestObserved", log); Assert.Contains("ResetReadyObserved", log);
        Assert.Contains("ResetVerificationObserved", log); Assert.Contains("ResetRequestClearWriteResponded", log);
        evidence.Save(plc, "Fresh Ready after dispatch required; pre-dispatch Ready never completes reset");
    }
    [Fact]
    public async Task ResetCompletesFromFreshReadyOneWithoutObservingZero()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        // The simulated PLC finishes while accepting the request, before its write response.
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 1 }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        await device.ResetAsync(deadline.Token);
        Assert.Equal(1, plc.ResetEdges); Assert.Equal(0, plc.Byte(2009));
        Assert.Equal(0, plc.ResetReadyZeroReads); Assert.True(plc.ResetReadyOneReads > 0);
        Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        var log = File.ReadAllText(Path.Combine(evidence.Root, "runtime.log"));
        Assert.DoesNotContain("ResetNotReadyObserved", log);
        Assert.DoesNotContain("readyFallingThenRisingObserved", log);
        Assert.Contains("\"readyReadAfterRequest\":true", log);
        Assert.Contains("\"notReadyObserved\":false", log);
        Assert.Contains("ResetCompleted", log);
        Assert.True(log.IndexOf("ResetRequestClearWriteResponded", StringComparison.Ordinal) <
            log.IndexOf("ResetVerificationObserved", StringComparison.Ordinal));
        evidence.Save(plc, "Fast PLC completion read after request clears it without an observed zero");
    }
    [Fact]
    public async Task ReadyCompletionClearsResetRequestEvenWhenPositionVerificationBlocks()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        // Actual XYZ remains 1.25: acknowledging PLC completion must not release startup.
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 1 };
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        var error = await Assert.ThrowsAsync<IOException>(() => device.ResetAsync(deadline.Token));
        Assert.Contains("StartupSafeZeroUnconfirmed", error.Message);
        Assert.Equal(0, plc.Byte(2009)); Assert.Equal(1, plc.ResetEdges);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Start(device, deadline.Token));
        Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        var log = File.ReadAllText(Path.Combine(evidence.Root, "runtime.log"));
        Assert.Contains("ResetRequestClearWriteResponded", log);
        Assert.Contains("ResetVerificationObserved", log);
        Assert.Contains("StartupSafeZeroUnconfirmed", log);
        Assert.DoesNotContain("\"outcome\":\"ResetCompleted\"", log);
        evidence.Save(plc, "PLC completion acknowledged; real nonzero XYZ still blocks startup");
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
        plc.SetByte(2009, 1); plc.SetByte(2008, 1); plc.SetByte(2001, 1);
        var error = await Assert.ThrowsAsync<IOException>(() => device.ResetAsync(deadline.Token));
        Assert.Equal("PreviousResetRequestNotReleased", error.Message);
        Assert.Equal(1, plc.Byte(2009)); Assert.Equal(1, plc.Byte(2008)); Assert.Equal(1, plc.Byte(2001));
        Assert.DoesNotContain("ResetPcRequestsObserved", ReadLiveLog(evidence.Root));
        Assert.Equal(0, plc.ResetEdges); Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        evidence.Save(plc, "Pending old reset is not cleared or retriggered");
    }
    [Fact]
    public async Task ResetReadyZeroTimesOutWithoutClearingOrReplaying()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 0 }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        using var resetDeadline = new CancellationTokenSource(700);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => device.ResetAsync(resetDeadline.Token));
        Assert.Equal(1, plc.Byte(2009)); Assert.Equal(1, plc.ResetEdges);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Start(device, deadline.Token));
        Assert.Equal(0, plc.StartEdges);
        var log = File.ReadAllText(Path.Combine(evidence.Root, "runtime.log"));
        Assert.Contains("ResetNotReadyObserved", log);
        Assert.DoesNotContain("ResetRequestClearWriteResponded", log);
        evidence.Save(plc, "Ready stays zero; timeout blocks start and does not clear or replay");
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
            using var captureEvidence = await CaptureWorkFixture.CreateAsync(capture);
            await device.FinishCaptureWindowAsync(capture, captureEvidence.Work, window, deadline.Token);
        }
        var clear = Assert.Single(plc.StartClears);
        Assert.Equal(0, clear.ZLow); Assert.Equal(0x4040, clear.ZHigh); // independent CDAB 3.0
        Assert.Equal(0, clear.ZRequest); Assert.Equal(1, clear.ZFeedback);
        Assert.Equal(0, plc.Byte(2007)); Assert.Equal(1, plc.StartEdges);
        Assert.Equal(3, plc.MotionEdges); // X, Y, Z only once; shared-register preservation is not another edge.
        evidence.Save(plc, "Full XYZ closure before startup clear; second same-target action reused");
    }
    [Fact]
    public async Task ResetArrivalCannotSubstituteForCompletedSameCoordinateAction()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture {ExerciseConfirmedOperations=true,ReadyOnReset=1};Zero(plc);
        await using var device=Device(plc,evidence);await device.StartAsync(deadline.Token);await device.ResetAsync(deadline.Token);await Start(device,deadline.Token);
        var result=await Devices.HandshakeClosureTests.Move(device,new("zero","1",0,0,"mm","OFFLINE_ONLY",0),"3D",deadline.Token);
        Assert.NotEqual(DeviceEventKind.Completed,result.Kind);Assert.Contains("AxisSameTargetWithoutCompletedAction",device.Failure);
        Assert.Equal(0,plc.MotionEdges);
        evidence.Save(plc,"System reset permits recovery but cannot create same-coordinate motion completion proof");
    }
    [Fact]
    public async Task RetainedArrivalAllowsChangedXThenSameYAndDetectionZAndFullReuse()
    {
        using var deadline = new CancellationTokenSource(20000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 1, RetainArrivalOnSystemReset = true }; Zero(plc);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token); await device.ResetAsync(deadline.Token);
        await Start(device, deadline.Token);
        foreach (var target in new[] { new FixedPoint("first","1",1,2,"mm","OFFLINE_ONLY",3),
            new FixedPoint("next","1",4,2,"mm","OFFLINE_ONLY",3), new FixedPoint("same","1",4,2,"mm","OFFLINE_ONLY",3) })
        {
            var result = await Devices.HandshakeClosureTests.Move(device,target,"Detection",deadline.Token);
            Assert.True(result.Kind == DeviceEventKind.Completed, result.ErrorCode ?? device.Failure);
            var position = Assert.Single(result.Evidence!.Positions);
            var window = Devices.HandshakeClosureTests.Window();
            var capture = await device.OpenCaptureWindowAsync(new(position.Correlation,CaptureRole.Detection,target,position,window),deadline.Token);
            using var captureEvidence = await CaptureWorkFixture.CreateAsync(capture);
            await device.FinishCaptureWindowAsync(capture,captureEvidence.Work,window,deadline.Token);
            foreach (var mb in new[] {2001,2002,2003}) Assert.Equal(0,plc.Byte(mb));
            foreach (var mb in new[] {6040,6042,6044}) Assert.Equal(1,plc.Word(mb));
        }
        Assert.Equal(4,plc.MotionEdges); // initial X/Y/Z, then X only, then no motion.
        evidence.Save(plc,"Retained arrival: changed X, reused Y/detection Z, repeated target; fresh completion evidence and PC request zero");
    }
    [Fact]
    public async Task OldArrivalAndEvenTargetCoordinatesCannotCompleteWithoutNewMoving()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations=true,ReadyOnReset=1,RetainArrivalOnSystemReset=true };Zero(plc);
        await using var device=Device(plc,evidence);await device.StartAsync(deadline.Token);await device.ResetAsync(deadline.Token);await Start(device,deadline.Token);
        plc.SuppressMotionTransition=true;
        var result=await Devices.HandshakeClosureTests.Move(device,new("target","1",1,2,"mm","OFFLINE_ONLY",0),"3D",deadline.Token,seconds:1);
        Assert.NotEqual(DeviceEventKind.Completed,result.Kind);
        Assert.Equal(1,plc.Word(6040));Assert.Equal(1,plc.Byte(2001));
        evidence.Save(plc,"Stale Arrived=1 and matching coordinates do not replace observing this action Moving -> Arrived");
    }
    [Fact]
    public async Task RotationRetainsArrivalAcrossTwoChangedAngles()
    {
        using var deadline=new CancellationTokenSource(15000);
        await using var evidence=new Evidence();
        await using var plc=new SiteProtocolTcpFixture {ExerciseConfirmedOperations=true,ReadyOnReset=1,RetainArrivalOnSystemReset=true};Zero(plc);
        await using var device=Device(plc,evidence);await device.StartAsync(deadline.Token);await device.ResetAsync(deadline.Token);await Start(device,deadline.Token);
        foreach(var angle in new[]{10,20})
        {
            var request=Devices.HandshakeClosureTests.Stage(device,PlcWorkflowStage.Rotate) with {
                TargetPurpose=RuntimePurposes.RealDeviceCommissioning,RotationTarget=new(angle,"R",.01,"OFFLINE-ROTATION")};
            var result=await device.RotateStageAsync(request,deadline.Token);
            Assert.True(result.Angle.Matched);Assert.Equal(0,plc.Byte(2000));Assert.Equal(1,plc.Word(6060));
        }
        Assert.Equal(2,plc.MotionEdges);
        evidence.Save(plc,"Rotation request cleared with arrival retained; next angle and same-angle reuse verified");
    }
    [Fact]
    public async Task PcRequestNotClearedStillBlocksEvenWhenArrivalIsCorrect()
    {
        using var deadline=new CancellationTokenSource(10000);
        await using var evidence=new Evidence();
        await using var plc=new SiteProtocolTcpFixture {ExerciseConfirmedOperations=true,ReadyOnReset=1,RetainArrivalOnSystemReset=true};Zero(plc);
        await using var device=Device(plc,evidence);await device.StartAsync(deadline.Token);await device.ResetAsync(deadline.Token);await Start(device,deadline.Token);
        plc.KeepXRequestAsserted=true;
        var result=await Devices.HandshakeClosureTests.Move(device,new("target","1",1,2,"mm","OFFLINE_ONLY",0),"3D",deadline.Token,seconds:2);
        Assert.NotEqual(DeviceEventKind.Completed,result.Kind);Assert.Equal(1,plc.Byte(2001));Assert.Equal(1,plc.Word(6040));
        Assert.Equal(2,plc.MotionEdges);
        evidence.Save(plc,"PC request staying one blocks completion; no action replay despite correct retained arrival");
    }
    [Fact]
    public async Task ScanAndGrabAxesRetainArrivalAcrossChangedAndSameTargets()
    {
        using var deadline=new CancellationTokenSource(20000);
        await using var evidence=new Evidence();
        await using var plc=new SiteProtocolTcpFixture {ExerciseConfirmedOperations=true,ReadyOnReset=1,RetainArrivalOnSystemReset=true};Zero(plc);
        await using var device=Device(plc,evidence);await device.StartAsync(deadline.Token);await device.ResetAsync(deadline.Token);await Start(device,deadline.Token);
        foreach(var z in new[]{1,2,2})
        {
            var target=new FixedPoint("scan","1",1,2,"mm","OFFLINE_ONLY",z);
            var result=await Devices.HandshakeClosureTests.Move(device,target,"E",deadline.Token);
            Assert.True(result.Kind==DeviceEventKind.Completed,result.ErrorCode??device.Failure);
            var position=Assert.Single(result.Evidence!.Positions);var window=Devices.HandshakeClosureTests.Window();
            var capture=await device.OpenCaptureWindowAsync(new(position.Correlation,CaptureRole.E,target,position,window),deadline.Token);
            using var captureEvidence = await CaptureWorkFixture.CreateAsync(capture);
            await device.FinishCaptureWindowAsync(capture,captureEvidence.Work,window,deadline.Token);
            Assert.Equal(0,plc.Byte(2004));Assert.Equal(1,plc.Word(6046));
        }
        foreach(var z in new[]{1,2,2})
        {
            var stage=Devices.HandshakeClosureTests.Stage(device,PlcWorkflowStage.UnloadPreparation);
            var result=await device.MoveStageAxesAsync(stage,new("grab","1",1,2,"mm","OFFLINE_ONLY",z),true,deadline.Token);
            Assert.True(result.Matched);Assert.Equal(0,plc.Byte(2005));Assert.Equal(1,plc.Word(6048));
        }
        Assert.Equal(6,plc.MotionEdges);
        evidence.Save(plc,"ScanZ and GrabZ changed targets and same-coordinate reuse preserve arrival with PC requests zero");
    }
    [Fact]
    public async Task ManualResetDoesNotPermitStart()
    {
        using var deadline = new CancellationTokenSource(10000);
        await using var evidence = new Evidence();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 1 }; Zero(plc);
        plc.SetByte(6016, 0);
        await using var device = Device(plc, evidence); await device.StartAsync(deadline.Token);
        await device.ResetAsync(deadline.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Start(device, deadline.Token));
        Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        evidence.Save(plc, "Manual reset succeeds but automatic start remains blocked");
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
