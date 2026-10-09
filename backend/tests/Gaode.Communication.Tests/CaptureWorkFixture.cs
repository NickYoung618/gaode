using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

// Declared Test capture with real files/SQLite; only protocol fixtures consume this.
internal sealed class CaptureWorkFixture : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(),"gaode-022-handshake-capture-"+Guid.NewGuid().ToString("N"));
    public CaptureWorkCommit Work { get; private set; } = null!;
    public static async Task<CaptureWorkFixture> CreateAsync(AcquisitionSession session)
    {
        var fixture = new CaptureWorkFixture();
        try
        {
            var options = CameraCaptureJournal.Prepare(fixture.root);
            var store = new MediaStore(fixture.root,new(65536,0,65536,65536),new(),1);
            var c = session.Request.Correlation;
            await using(var db=new Station01DbContext(options))
            { db.Runs.Add(new() {RunId=c.RunId,RequestId=c.RunId.ToString(),SubjectId="Test",ContextJson="{\"purpose\":\"Test\"}",State=RunState.Created,Revision=1,CreatedUtc=DateTimeOffset.UtcNow}); await db.SaveChangesAsync(); }
            var request = new CaptureRequest(new(c.RunId,Guid.NewGuid(),c.Attempt,c.SessionId,c.SnapshotId,"1","Test",1,long.MaxValue,session.Request.Window.ClockId),
                Guid.NewGuid(),session.Request.Role,"Test","1",null,null,"TestCamera",null,Guid.NewGuid(),4)
                {AcquisitionSessionId=session.SessionId,AcquisitionOperationId=c.OperationId};
            using var captureLimit = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var received = await new CameraAcquisitionService(new Camera(),store).ReceiveAsync(request,captureLimit.Token);
            MediaRef media;
            using(store.ReserveCapture(request.CaptureId,"Detection",4))
                media=(await store.SaveCaptureAsync(c.RunId,request.CaptureId,"Detection","1","1",received.Bytes,"bin","Test",received.Fact,default)) with {Purpose="Test"};
            await using var writer=new TraceWriter(options,TimeProvider.System,8);
            var revision=1L;
            async Task<CommitReceipt> Save(WriteKind kind,object payload)
            {
                var json=JsonSerializer.Serialize(payload,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var receipt=await writer.SubmitCritical(new(Guid.NewGuid(),c.RunId,revision,kind,json,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))))).Completion.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.Equal(CommitState.Committed,receipt.State); revision=receipt.CommittedRevision!.Value;return receipt;
            }
            var image=await Save(WriteKind.Media,media);
            var fact=await Save(WriteKind.CaptureFact,new {mediaId=media.MediaId,captureFact=received.Fact});
            await store.MarkCommittedAsync(media,default);
            var completion=await CaptureCompletionEvidence.FromCommittedAsync(new TraceQuery(options),store,request,received,media,image,fact,default);
            var wrongCall=Guid.NewGuid();
            await Save(WriteKind.AlgorithmIntent,new AlgorithmIntentPayload(wrongCall,Guid.NewGuid(),1,c.RunId,request.CaptureId,[media.MediaId],
                "1","1","1","Test:negative-proof","1","Test",request.Envelope.SessionId,request.Envelope.ClockId,1,2,1,"Test:negative-proof"));
            var wrongType=await Save(WriteKind.AlgorithmFact,new AlgorithmFactPayload(wrongCall,AlgorithmState.Error,false,"{}","Test:negative-proof","NotDispatched"));
            await Assert.ThrowsAsync<InvalidDataException>(() => CaptureCompletionEvidence.FromCommittedAsync(new TraceQuery(options),store,request,received,media,image,wrongType,default));
            await Assert.ThrowsAsync<InvalidDataException>(() => CaptureCompletionEvidence.FromCommittedAsync(new TraceQuery(options),store,request,received,media,image,fact with {State=CommitState.CommitUnknown},default));
            Assert.Throws<InvalidDataException>(()=>completion.ForWindow(session with {SessionId=Guid.NewGuid()},c.TrayId??Guid.NewGuid()));
            fixture.Work=completion.ForWindow(session,c.TrayId ?? Guid.NewGuid()); return fixture;
        }
        catch { fixture.Dispose();throw; }
    }
    private sealed class Camera : ICapturePort
    {
        public int TriggerCount(CaptureRole role)=>1;
        public long ConnectionEpoch=>1;
        public ValueTask RequestCaptureAsync(CaptureRequest request,Action<CaptureEvent> onEvent,CancellationToken token)
        {
            var fact=new CorrelatedCaptureFact(request.Envelope.RunId,request.CaptureId,request.Envelope.OperationId,1,
                AcquisitionContract.RequestedSettingsDigest(request),"Test",ComponentExecutionOrigin.Unknown,ComponentExecutionOrigin.Unknown,
                CaptureApplicationState.NotApplied,null,false,["Test:declared-capture"]);
            onEvent(new(request,CaptureEventKind.Ended,1));onEvent(new(request,CaptureEventKind.MediaTaken,1,[1,2,3,4],"bin"){Fact=fact});
            return ValueTask.CompletedTask;
        }
    }
    public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(root))Directory.Delete(root,true); }
}
