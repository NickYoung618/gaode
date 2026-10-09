using Gaode.Application.Acquisition;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed partial class CameraBusinessRegressionTests
{
    [Fact]
    public async Task CoordinatorPreservesIntentMediaIndexAndRunScopedFConstraint()
    {
        var root=Path.Combine(Path.GetTempPath(),"gaode-review-acquisition-"+Guid.NewGuid().ToString("N"));
        var options=CameraCaptureJournal.Prepare(root);
        try
        {
            await using var writer=new TraceWriter(options,TimeProvider.System,32);
            var ports=new DeclaredPorts(false,false,null,false);
            var media=new MediaStore(root,new(65536,0,65536,65536),new MediaLeaseRegistry(),1);
            var coordinator=new AcquisitionCoordinator(ports,media,new OperationIngress(new DeadlineScheduler(TimeProvider.System,"review-clock")),new TraceQuery(options));
            var budget=ReviewBusinessData.Budget();
            var config=new FrozenConfiguration(ReviewBusinessData.Motion(),budget,ReviewBusinessData.Read<SimulationProfile>("simulation.normal.json"),
                "{}","{}","{}","test-public","test-budget","test-simulation",new Dictionary<string,string>(),"review-snapshot");
            RunExecution Run()=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid().ToString(),"Test","{}",config,writer,TimeProvider.System,Guid.NewGuid(),"review-clock");
            var first=Run();
            var second=Run();
            await using(var db=new Station01DbContext(options))
            {
                foreach(var run in new[]{first,second}) db.Runs.Add(new RunEntity {RunId=run.RunId,RequestId=run.RequestId,SubjectId=run.SubjectId,ContextJson=run.ContextJson,CreatedUtc=DateTimeOffset.UtcNow});
                await db.SaveChangesAsync();
            }
            var point=new FixedPoint("review","1",1,2,"mm","test-frame",3);
            var a=await coordinator.CaptureAsync(first,CaptureRole.F,point,null,null,"F","TestLight",1024,default);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>coordinator.CaptureAsync(first,CaptureRole.F,point,null,null,"F","TestLight",1024,default));
            var b=await coordinator.CaptureAsync(second,CaptureRole.F,point,null,null,"F","TestLight",1024,default);
            var c=await coordinator.CaptureAsync(second,CaptureRole.ThreeD,point,null,null,"3D","TestLight",1024,default);
            Assert.Equal(2,ports.TriggerCount(CaptureRole.F)); Assert.Equal(1,ports.TriggerCount(CaptureRole.ThreeD));
            var index=new CameraCaptureJournal(options);
            var restored=new MediaStore(root,new(65536,0,65536,65536),new MediaLeaseRegistry(),1);
            await index.RestoreAsync(restored,default);
            foreach(var reference in new[]{a.Media,b.Media,c.Media})
            {
                Assert.True(media.IsReady(reference.MediaId));Assert.True(restored.IsReady(reference.MediaId));
                Assert.Equal("Test",reference.Source);
                var writes=await new TraceQuery(options).GetWritesAsync(reference.RunId,default);
                var request=Assert.Single(ports.Requests,r=>r.CaptureId==reference.CaptureId);
                var intent=Assert.Single(writes,w=>w.WriteId==request.IntentWriteId);
                var mediaWrite=Assert.Single(writes,w=>w.Kind==WriteKind.Media&&w.PayloadJson.Contains(reference.MediaId.ToString()));
                Assert.Equal(CommitState.Committed,intent.State);Assert.True(intent.Revision<mediaWrite.Revision);
                Assert.Contains(writes,w=>w.Kind==WriteKind.CaptureFact&&w.PayloadJson.Contains(reference.CaptureId.ToString()));
                Assert.Equal(reference.RunId,request.Envelope.RunId);
            }
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root,true); }
    }
}

