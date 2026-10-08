using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Cameras;
using Xunit;
using Gaode.Communication.Tests.Devices;
namespace Gaode.Communication.Tests;
public sealed class CommissioningLightModeTests
{
    [Theory]
    [InlineData("Simulated", false, true)]
    [InlineData("Real", false, false)]
    [InlineData("Real", true, true)]
    public async Task ExplicitModeControlsLightButNeverSkipsCamera(string mode, bool realAdapter, bool expectedCapture)
    {
        var sdk=new Camera(); var light=new Light(realAdapter); var port=new CameraCaptureAdapter(sdk,light);
        var request=new CaptureRequest(new(Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),"offline","1",RuntimePurposes.RealDeviceCommissioning,1,2,"offline"),
            Guid.NewGuid(),CaptureRole.Detection,"point","1",null,null,"C","light",Guid.NewGuid(),1024)
            {LightExecution=new("light-execution/1",mode), DetectionSettings=mode=="Simulated"?new("p",100,1,[0,0,1,1]):new("p",100,1,[0,0,1,1],"ch",20,0)};
        var events=new List<CaptureEvent>(); await port.RequestCaptureAsync(request,events.Add,default);
        Assert.Equal(expectedCapture?1:0,sdk.Captures);
        if(!expectedCapture) {Assert.Contains(events,e=>e.Kind==CaptureEventKind.Unknown&&e.ErrorCode=="RealLightAdapterNotConfigured");Assert.Empty(light.Calls);return;}
        var gate=new CaptureEvidenceGate();foreach(var e in events)gate.Observe(e);
        var fact=gate.TakeFact(request,1);Assert.Equal(100,fact.ActualCameraSettings!.ExposureUs);
        Assert.Equal(mode=="Real",fact.PhysicalLightApplied);Assert.Equal(request.LightExecution,fact.LightExecution);
        if(mode=="Simulated") {Assert.Empty(light.Calls);Assert.Equal(CaptureApplicationState.NotApplied,fact.LightApplicationState);}
        else Assert.Equal(new[]{"brightness:20","on","off"},light.Calls);
    }
    [Fact]
    public void MissingLightParametersOnlyAllowedInExplicitVirtualModeAndFPositionRoundTrips()
    {
        var original=Recipe011Data.ForSlots(2,1);
        var profiles=original.CaptureProfiles.ToDictionary(p=>p.Key,p=>p.Value with {Settings=p.Value.Settings with {LightChannel=null,BrightnessPercent=null,SettleMs=null}});
        var recipe=original with {LightExecution=new("light-execution/1","Simulated"),CaptureProfiles=profiles,
            CommissioningFPosition=new("commissioning-f-position/1",12,34)};
        RecipeDefinitionValidator.Validate(recipe);
        var restored=RecipeDefinitionSerialization.Deserialize(RecipeDefinitionSerialization.Serialize(recipe));
        Assert.DoesNotContain("isSimulated",RecipeDefinitionSerialization.Serialize(recipe));Assert.Equal(recipe.LightExecution,restored.LightExecution);Assert.Equal(recipe.CommissioningFPosition,restored.CommissioningFPosition);
        Assert.ThrowsAny<Exception>(()=>RecipeDefinitionValidator.Validate(recipe with {LightExecution=new("light-execution/1","Real")}));
        Assert.ThrowsAny<Exception>(()=>RecipeDefinitionValidator.Validate(recipe with {LightExecution=null}));
        Assert.ThrowsAny<Exception>(()=>RecipeDefinitionValidator.Validate(recipe with {CaptureProfiles=profiles.ToDictionary(p=>p.Key,p=>p.Value with {Settings=p.Value.Settings with {ExposureUs=0}})}));
        Assert.DoesNotContain("lightExecution",RecipeDefinitionSerialization.Serialize(original));
    }
    private sealed class Light(bool real):ILightGateway
    {
        public List<string> Calls {get;}=[];
        public ComponentExecutionOrigin Origin=>new(real?ComponentEvidenceSource.Real:ComponentEvidenceSource.Simulated,"OFFLINE-port","DeclaredNoPhysicalLight");
        public Task SetBrightnessAsync(string b,string c,int value,CancellationToken ct=default){Calls.Add("brightness:"+value);return Task.CompletedTask;}
        public Task SetAsync(string b,bool enabled,CancellationToken ct=default){Calls.Add(enabled?"on":"off");return Task.CompletedTask;}
    }
    private sealed class Camera:ICameraSdkGateway
    {
        public int Captures; public long ConnectionEpoch=>1;
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public Task OpenAsync(string b,CancellationToken ct=default)=>Task.CompletedTask;
        public Task ConfigureAsync(string b,int e,double g,CancellationToken ct=default)=>Task.CompletedTask;
        public Task<CameraFrame> TriggerAsync(string b,string v,CancellationToken ct=default)=>throw new Exception("MustApplyCameraSettings");
        public Task<CameraFrame> CaptureConfiguredAsync(string b,string v,CameraImagingSettings settings,string digest,CancellationToken ct=default)
        {
            Captures++; var at=DateTimeOffset.UtcNow;
            return Task.FromResult(new CameraFrame([1,2,3],"png","image/png",1){ActualSettings=new(settings.ExposureUs,settings.Gain??1,1,1,0,0),
                Metadata=new("capture-frame/1","C","OFFLINE","","","",Guid.NewGuid(),1,0,1,at,at,1,1,"OFFLINE",3,new Dictionary<string,string>(),[])});
        }
    }
}
