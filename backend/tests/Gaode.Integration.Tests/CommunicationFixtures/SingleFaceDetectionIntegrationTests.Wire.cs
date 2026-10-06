using System.Text.Json;
using Xunit;
namespace Gaode.Integration.Tests.Devices;

// Exact active wire obligations from migration T35. Business assertions execute
// in the protected companion source against actual Worker/SQLite/media evidence.
public sealed partial class SingleFaceDetectionIntegrationTests
{
    [Fact]
    public async Task AbThenCdExecuteThroughVirtualPlcWorkerMediaAndSqlite()
    {
        await AssertWire(await RunAsync("Q01",["P01"],"AB",2),2,false);
        // CD obligations are executed once by 010 V05 FullRun and its independent expectations.
    }
    [Theory]
    [InlineData("DETECTION-COMPONENT/WrongArrival",FailureKind.WrongArrival)]
    [InlineData("DETECTION-COMPONENT/MediaSave",FailureKind.MediaSave)]
    [InlineData("DETECTION-COMPONENT/CaptureObservationLost",FailureKind.ReleaseUnavailable)]
    [InlineData("DETECTION-COMPONENT/SaveWindowExpiry",FailureKind.SaveWindowExpiry)]
    public async Task NecessaryFailureStopsBeforeNextProductMove(string caseId,FailureKind failure)
    {
        Assert.StartsWith("DETECTION-COMPONENT/",caseId);
        await AssertWire(await RunAsync("Q01",["P01"],"AB",2,failure),2,true);
    }

    private static async Task AssertWire(string root,int captures,bool failed)
    {
        using var wire=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"component-wire.json")));
        var changes=wire.RootElement.GetProperty("changes").EnumerateArray().ToArray();
        long[] Events(string name,string value)=>changes.Where(x=>x.GetProperty("Name").GetString()==name&&
            x.GetProperty("Current").GetString()==value).Select(x=>x.GetProperty("Sequence").GetInt64()).Order().ToArray();
        var moves=Events("XY_Move_Cmd","2");
        if(failed) {Assert.Single(moves);return;}
        var began=Events("Inspection_Status","1");var ended=Events("Inspection_Status","2");
        var reset=Events("Z_Reset_Status","2");var cleared=Events("Inspection_Status","0");
        Assert.Equal(captures,moves.Length);Assert.Equal(captures,began.Length);
        Assert.Equal(captures,ended.Length);Assert.Equal(captures,reset.Length);Assert.Equal(captures,cleared.Length);
        for(var i=0;i<captures;i++) {
            Assert.True(moves[i]<began[i]&&began[i]<ended[i]&&ended[i]<reset[i]&&reset[i]<cleared[i]);
            if(i+1<captures)Assert.True(cleared[i]<moves[i+1]);
        }
    }
}
