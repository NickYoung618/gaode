using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Cameras;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Gaode.Communication.Tests;

public sealed class CameraWorkerLifecycleTests(ITestOutputHelper output)
{
    private static string ReadDiagnostic(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
    [Fact]
    public async Task ConfiguredCapturePersistsSeparateCameraAndSimulatedLightFacts()
    {
        await using var fixture = new Fixture("A");
        await fixture.Gateway.StartAsync(default);
        var root = Path.Combine(fixture.Root, "configured-store");
        var journal = new CameraCaptureJournal(CameraCaptureJournal.Prepare(root));
        var media = new MediaStore(root, new MediaCapacity(65536, 0, 65536, 65536), new MediaLeaseRegistry(), 1);
        var light = new SimulatedLightGateway();
        var camera = new CameraCaptureAdapter(fixture.Gateway, light);
        var initial = Request("A");
        var request = initial with { Envelope = initial.Envelope with { Purpose = "RealDeviceCommissioning" },
            LightBindingId = "fixture-light", DetectionSettings = new("fixture", 1000, 1, [0, 0, 2, 2], "channel-a", 50, 1) };
        var reference = await new CameraAcquisitionService(camera, media).CaptureAsync(request, journal, default);
        Assert.Equal("FileCompleted", reference.StorageState);
        var state = light.State("fixture-light")!;
        Assert.Equal("channel-a", state.Channel); Assert.Equal(50, state.BrightnessPercent); Assert.False(state.Enabled);
        var sidecar = Assert.Single(Directory.GetFiles(root, "*.metadata.json", SearchOption.AllDirectories));
        var stored = JsonDocument.Parse(File.ReadAllText(sidecar)).RootElement;
        var fact = stored.GetProperty("fact").Deserialize<CorrelatedCaptureFact>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(CaptureApplicationState.Applied, fact.CameraApplicationState);
        Assert.Equal(CaptureApplicationState.ConfiguredOnly, fact.LightApplicationState);
        Assert.Equal(Gaode.Domain.Station01.ComponentEvidenceSource.Simulated, fact.LightOrigin.Source);
        Assert.False(fact.PhysicalLightApplied);
        Assert.Equal(1000, fact.ActualCameraSettings!.ExposureUs);
        Assert.Equal(AcquisitionContract.RequestedSettingsDigest(request), fact.RequestedSettingsDigest);
        // Removing new fields from a historical record must never fabricate Applied.
        var historical = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(fact))!.AsObject();
        historical.Remove("CameraApplicationState"); historical.Remove("LightApplicationState"); historical.Remove("ActualCameraSettings");
        var old = historical.Deserialize<CorrelatedCaptureFact>()!;
        Assert.Equal(CaptureApplicationState.Unknown, old.CameraApplicationState);
        Assert.Equal(CaptureApplicationState.Unknown, old.LightApplicationState);
    }
    [Fact]
    public async Task AtomicSettingsHaveSeparateReadbacksAndFreshFramesAndNormalRestoration()
    {
        await using var fixture = new Fixture("A");
        await fixture.Gateway.StartAsync(default);
        var a = await fixture.Gateway.CaptureConfiguredAsync("A", "1", new(1000, 1, [0, 0, 2, 2]), "settings-a");
        var b = await fixture.Gateway.CaptureConfiguredAsync("A", "2", new(2000, 2, [0, 0, 2, 2]), "settings-b");
        Assert.Equal(1000, a.ActualSettings!.ExposureUs); Assert.Equal(1, a.ActualSettings.Gain);
        Assert.Equal(2000, b.ActualSettings!.ExposureUs); Assert.Equal(2, b.ActualSettings.Gain);
        Assert.True(b.Metadata!.FrameId > a.Metadata!.FrameId);
        Assert.Equal(a.Metadata.WorkerSessionId, b.Metadata.WorkerSessionId);
        Assert.Equal("1000", a.Metadata.ActualParameters["ExposureTime"]);
        Assert.Equal("2000", b.Metadata.ActualParameters["ExposureTime"]);
        await fixture.Gateway.DisposeAsync();
        Assert.True(JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Root, "restoration.json"))).RootElement.GetProperty("restored").GetBoolean());
        Assert.Equal(2, File.ReadAllLines(Path.Combine(fixture.Root, "triggers.txt")).Length);
    }

    [Theory]
    [InlineData("settings-unsupported")]
    [InlineData("settings-readback-fail")]
    public async Task SettingsRejectedBeforeTriggerFaultSessionAndNeverReplay(string mode)
    {
        await using var fixture = new Fixture("A");
        await fixture.Mode(mode); await fixture.Gateway.StartAsync(default);
        var logger = new ControlledCommissioningTests.FileLogger(Path.Combine(fixture.Root, "capture-failure.log"));
        using var diagnostics = new Gaode.Infrastructure.Diagnostics.RuntimeDiagnosticLogging(logger);
        var root = Path.Combine(fixture.Root, "store");
        var journal = new CameraCaptureJournal(CameraCaptureJournal.Prepare(root));
        var media = new MediaStore(root, new MediaCapacity(65536, 0, 65536, 65536), new MediaLeaseRegistry(), 1);
        var service = new CameraAcquisitionService(new CameraCaptureAdapter(fixture.Gateway, new SimulatedLightGateway()), media);
        var initial = Request("A");
        var request = initial with { Envelope = initial.Envelope with { Purpose = "RealDeviceCommissioning" },
            LightBindingId = "fixture-light", DetectionSettings = new("fixture", 1000, 1, [0, 0, 2, 2], "channel-a", 50, 1) };
        await Assert.ThrowsAsync<IOException>(() => service.CaptureAsync(request, journal, default));
        Assert.Empty(await journal.ListCommittedAsync(default));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "triggers.txt")));
        Assert.Equal("Faulted", Assert.Single(fixture.Gateway.Status).State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gateway.CaptureConfiguredAsync("A", "1", new(1000, 1), "settings"));
        var log = ReadDiagnostic(Path.Combine(fixture.Root, "state", "A.host.jsonl"));
        Assert.Contains("Faulted_NoAutomaticReplay", log);
        Assert.Contains(mode == "settings-readback-fail" ? "ParameterReadbackMismatch" : "CameraParameterUnsupported", log);
        File.WriteAllText(Path.Combine(ControlledCommissioningTests.Inputs.RepoRoot(), "specs", "020-real-device-commissioning", "evidence", "stage-b", "software", mode + ".jsonl"), log);
        var failure = File.ReadAllText(logger.Path);
        Assert.Contains("FailedOrUnknown_NoReplay", failure); Assert.Contains("Error", failure);
        Assert.Contains(request.Envelope.RunId.ToString(), failure); Assert.Contains(request.CaptureId.ToString(), failure);
        File.WriteAllText(Path.Combine(ControlledCommissioningTests.Inputs.RepoRoot(), "specs", "020-real-device-commissioning", "evidence", "stage-b", "software", mode + ".log"), failure);
    }
    [Fact]
    public async Task ActualWorkerExitRevokesReadyAndRecoveryRequiresFaultAndNewSession()
    {
        await using var fixture = new Fixture("A");
        var gateway = fixture.Gateway;
        await gateway.StartAsync(default);
        var initial = Assert.Single(gateway.Status); Assert.Equal("Ready", initial.State);
        await Assert.ThrowsAsync<CameraRecoveryRejectedException>(() => gateway.RecoverAsync("A", default));
        Assert.Equal(initial, Assert.Single(gateway.Status));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "exit"), "exit actual child");
        using var deadline = new CancellationTokenSource(5000);
        // Do not poll Snapshot: prove that the Exited event itself persists the fault.
        var log = Path.Combine(fixture.Root, "state", "A.host.jsonl");
        while (!ReadDiagnostic(log).Contains("CameraWorkerExitedUnexpectedly")) await Task.Delay(20, deadline.Token);
        var failed = Assert.Single(gateway.Status);
        Assert.Equal("Faulted", failed.State); Assert.Null(failed.ProcessId); Assert.NotNull(failed.Error); Assert.Equal(0, failed.MaxBytes);
        output.WriteLine("Offline actual process exit evidence: " + ReadDiagnostic(log));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.TriggerAsync("A", "1"));
        Assert.False(File.Exists(Path.Combine(fixture.Root,"triggers.txt")));
        File.Delete(Path.Combine(fixture.Root, "exit"));
        await fixture.Mode("init-fail");
        var failure = await Assert.ThrowsAsync<CameraRecoveryFailedException>(() => gateway.RecoverAsync("A", default));
        Assert.Equal("Faulted", failure.Status.State);
        output.WriteLine("Recovery failed (no successful Task result): " + JsonSerializer.Serialize(failure.Status));
        await fixture.Mode("normal");
        await gateway.RecoverAsync("A", default);
        var recovered = Assert.Single(gateway.Status);
        Assert.Equal("Ready", recovered.State); Assert.NotEqual(initial.SessionId, recovered.SessionId); Assert.Equal(2, recovered.OpenCount);
        output.WriteLine("Recovery succeeded with new session: " + JsonSerializer.Serialize(recovered));
        await Task.Delay(50); // Old process exit callbacks queued behind recovery must not change the new session.
        Assert.Equal(recovered, Assert.Single(gateway.Status));
        await fixture.Mode("old-session");
        await Assert.ThrowsAsync<InvalidDataException>(() => gateway.TriggerAsync("A", "1"));
        Assert.Equal("Faulted", Assert.Single(gateway.Status).State);
        Assert.Single(File.ReadAllLines(Path.Combine(fixture.Root,"triggers.txt")));
        await gateway.DisposeAsync();
        await Assert.ThrowsAsync<CameraRecoveryRejectedException>(() => gateway.RecoverAsync("A", default));
        Assert.Equal("Stopped", Assert.Single(gateway.Status).State);
    }

    [Theory]
    [InlineData("A", "bad-role")]
    [InlineData("A", "bad-size")]
    [InlineData("3D", "missing-ir")]
    public async Task InvalidFrameThroughActualProcessAndFormalServiceCannotCommit(string role, string mode)
    {
        await using var fixture = new Fixture(role);
        await fixture.Mode(mode); await fixture.Gateway.StartAsync(default);
        var storeRoot = Path.Combine(fixture.Root,"store");
        var journal = new CameraCaptureJournal(CameraCaptureJournal.Prepare(storeRoot));
        var store = new MediaStore(storeRoot,new MediaCapacity(65536,0,65536,65536),new MediaLeaseRegistry(),1);
        var camera = new CameraCaptureAdapter(fixture.Gateway);
        var service = new CameraAcquisitionService(camera,store);
        var request = Request(role);
        await Assert.ThrowsAsync<IOException>(()=>service.CaptureAsync(request,journal,default));
        Assert.Empty(await journal.ListCommittedAsync(default));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(storeRoot,"media"),"*",SearchOption.AllDirectories));
        Assert.Single(File.ReadAllLines(Path.Combine(fixture.Root,"triggers.txt")));
    }

    [Fact]
    public async Task FormalServiceIndexFailureRetainsChargedPayloadWithoutPublishingMedia()
    {
        await using var fixture = new Fixture("A");
        await fixture.Gateway.StartAsync(default);
        var root=Path.Combine(fixture.Root,"store");
        var options=CameraCaptureJournal.Prepare(root); var journal=new CameraCaptureJournal(options);
        await using(var db=new Station01DbContext(options))
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_media BEFORE INSERT ON Media BEGIN SELECT RAISE(ABORT, 'review service index failure'); END;");
        var capacity=new MediaCapacity(8192,0,4096,4096);
        var store=new MediaStore(root,capacity,new MediaLeaseRegistry(),1);
        var service=new CameraAcquisitionService(new CameraCaptureAdapter(fixture.Gateway),store);
        var logger = new ControlledCommissioningTests.FileLogger(Path.Combine(fixture.Root, "save-failure.log"));
        using var diagnostics = new Gaode.Infrastructure.Diagnostics.RuntimeDiagnosticLogging(logger);
        var request = Request("A");
        await Assert.ThrowsAsync<DbUpdateException>(()=>service.CaptureAsync(request,journal,default));
        var failure = File.ReadAllText(logger.Path);
        Assert.Contains("CameraCapture", failure); Assert.Contains("FailedOrUnknown_NoReplay", failure);
        Assert.Contains("Error", failure); Assert.Contains("review service index failure", failure);
        Assert.Contains(request.Envelope.RunId.ToString(), failure); Assert.Contains(request.CaptureId.ToString(), failure);
        File.WriteAllText(Path.Combine(ControlledCommissioningTests.Inputs.RepoRoot(), "specs", "020-real-device-commissioning", "evidence", "stage-b", "software", "save-failure.log"), failure);
        Assert.Equal(4,capacity.FilesUsed); Assert.Equal(0,capacity.MemoryUsed);
        Assert.Empty(await journal.ListCommittedAsync(default));
        var reference=Assert.Single(Directory.GetFiles(Path.Combine(root,"media"),"*.GalaxyRaw",SearchOption.AllDirectories));
        Assert.Equal(4,new FileInfo(reference).Length);
        var restartedCapacity=new MediaCapacity(8192,0,4,4);
        var restarted=new MediaStore(root,restartedCapacity,new MediaLeaseRegistry(),1);
        await journal.RestoreAsync(restarted,default);
        Assert.Equal(4,restartedCapacity.FilesUsed);
        Assert.Throws<InvalidOperationException>(()=>restarted.ReserveCapture(Guid.NewGuid(),"A",4));
        Assert.Single(File.ReadAllLines(Path.Combine(fixture.Root,"triggers.txt")));
    }

    [Fact]
    public async Task RecoveryDuringCaptureIsRejectedAndNormalShutdownIsNotUnexpectedExit()
    {
        await using var fixture=new Fixture("A");
        await fixture.Gateway.StartAsync(default); var initial=Assert.Single(fixture.Gateway.Status);
        await fixture.Mode("hold"); var capture=fixture.Gateway.TriggerAsync("A","1");
        using var deadline=new CancellationTokenSource(5000);
        while(!File.Exists(Path.Combine(fixture.Root,"triggers.txt"))) await Task.Delay(10,deadline.Token);
        await Assert.ThrowsAsync<CameraRecoveryRejectedException>(()=>fixture.Gateway.RecoverAsync("A",default));
        await capture; Assert.Equal(initial.SessionId,Assert.Single(fixture.Gateway.Status).SessionId);
        await fixture.Gateway.DisposeAsync();
        Assert.Equal("Stopped",Assert.Single(fixture.Gateway.Status).State);
        Assert.DoesNotContain("CameraWorkerExitedUnexpectedly",File.ReadAllText(Path.Combine(fixture.Root,"state","A.host.jsonl")));
    }

    private static CaptureRequest Request(string role) => new(new PortEnvelope(Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),"fixture","1","Test",1,long.MaxValue,"fixture-clock"),
        Guid.NewGuid(),role=="3D"?CaptureRole.ThreeD:CaptureRole.Detection,"fixture","1",null,null,role,null,Guid.NewGuid(),4096);

    private sealed class Fixture : IAsyncDisposable
    {
        public string Root {get;}=Path.Combine(Path.GetTempPath(),"gaode-worker-review-"+Guid.NewGuid().ToString("N"));
        public PersistentCameraGateway Gateway {get;}
        public Fixture(string role)
        {
            Directory.CreateDirectory(Root);
            var site=Path.Combine(Root,"site.json");
            File.WriteAllText(site,JsonSerializer.Serialize(new {Cameras=new[]{new CameraBinding(role,role=="3D"?"3D":"2D","fixture-serial","001122334455")}}));
            var worker=Path.Combine(AppContext.BaseDirectory,"CameraWorkerFixture","Gaode.CameraWorkerFixture.exe");
            Gateway=new(new(site,worker,Root,Root,Path.Combine(Root,"state"),5000,5000,3000));
        }
        public Task Mode(string value)=>File.WriteAllTextAsync(Path.Combine(Root,"mode.txt"),value);
        public async ValueTask DisposeAsync()
        {
            await Gateway.DisposeAsync(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(Root,true);
        }
    }
}
