using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Cameras;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class CameraWorkerLifecycleTests
{
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
        while (!File.ReadAllText(log).Contains("CameraWorkerExitedUnexpectedly")) await Task.Delay(20, deadline.Token);
        var failed = Assert.Single(gateway.Status);
        Assert.Equal("Faulted", failed.State); Assert.Null(failed.ProcessId); Assert.NotNull(failed.Error); Assert.Equal(0, failed.MaxBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.TriggerAsync("A", "1"));
        Assert.False(File.Exists(Path.Combine(fixture.Root,"triggers.txt")));
        File.Delete(Path.Combine(fixture.Root, "exit"));
        await fixture.Mode("init-fail");
        var failure = await Assert.ThrowsAsync<CameraRecoveryFailedException>(() => gateway.RecoverAsync("A", default));
        Assert.Equal("Faulted", failure.Status.State);
        await fixture.Mode("normal");
        await gateway.RecoverAsync("A", default);
        var recovered = Assert.Single(gateway.Status);
        Assert.Equal("Ready", recovered.State); Assert.NotEqual(initial.SessionId, recovered.SessionId); Assert.Equal(2, recovered.OpenCount);
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
        await Assert.ThrowsAsync<DbUpdateException>(()=>service.CaptureAsync(Request("A"),journal,default));
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
