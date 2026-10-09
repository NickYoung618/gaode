using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Host.Composition;
using Gaode.Infrastructure.Configuration;
using Gaode.Infrastructure.Integrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class RealAlgorithmConfigurationTests
{
    [Fact]
    public void ActualOptionsAndRegistrationKeepMissingRealDeliveryUnreadyWithoutSimulatedConsumer()
    {
        using var inputs = new ControlledCommissioningTests.Inputs();
        var descriptor = Configure(inputs);
        var options = Station01OptionsReader.Read(Keys(inputs, descriptor));
        Assert.Equal(descriptor, options.RealAlgorithmConfigPath);
        var services = new ServiceCollection(); services.AddLogging(); services.AddStation01(options);
        using var provider = services.BuildServiceProvider();
        var loaded = provider.GetRequiredService<RealAlgorithmLoad>();
        Assert.False(loaded.IsReady); Assert.Equal("ComponentMissing", loaded.Readiness);
        Assert.All(loaded.Components, c => { Assert.True(Path.IsPathFullyQualified(c.Path)); Assert.Equal("ComponentMissing",c.State); });
        Assert.IsType<NotIntegratedAlgorithm>(provider.GetRequiredService<IAlgorithmPort>());
        Assert.False(provider.GetRequiredService<IAlgorithmPort>().Origin.IsKnown);
        Assert.Null(provider.GetService<ICommissioningRunInputs>());
        Assert.Equal(RuntimePurposes.RealDeviceCommissioning, provider.GetRequiredService<Station01RuntimeOptions>().StoreProfile);
        Assert.False(Directory.Exists(options.TestRoot)); // no storage or hardware is opened by this configuration assertion
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStation01(options with { Mode="Production" }));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStation01(options with { Mode="FullSimulation" }));
        Assert.False(Directory.Exists(options.TestRoot));
        var loader = new ConfigurationLoader(options.ConfigRoot,options.SchemaRoot);
        var p = loader.LoadPublic(options.PublicReference); var b = loader.LoadBudget(options.BudgetReference);
        var validation = provider.GetRequiredService<PublicConfigurationValidator>().Validate(p.Value,b.Value,null,
            fullSimulation:false,realDeviceCommissioning:true,externalPlcProvider:"Real",realAlgorithm:loaded.Configuration.Value);
        Assert.Contains("TrayPoseNotConfigured",validation.AlgorithmIssues);
        var old = ConfigurationFreezer.Freeze(p,b,null,new Dictionary<string,string>(),null,loaded.Configuration);
        var changed = JsonNode.Parse(File.ReadAllText(descriptor))!;
        changed["version"] = "2"; changed["modules"]![0]!["modelVersion"] = "2";
        File.WriteAllText(descriptor,changed.ToJsonString());
        var nextLoad = RealAlgorithmConfigurationLoader.Load(descriptor,Digest(descriptor),options.SimulationReference,options.PublicReference,options.BudgetReference);
        var next = ConfigurationFreezer.Freeze(p,b,null,new Dictionary<string,string>(),null,nextLoad.Configuration);
        Assert.NotEqual(old.SnapshotId,next.SnapshotId);
        Assert.Equal("1",JsonDocument.Parse(old.RealAlgorithmJson!).RootElement.GetProperty("modules")[0].GetProperty("modelVersion").GetString());
        Assert.Equal(loaded.Configuration.Digest,old.RealAlgorithmDigest);
        Assert.Equal("2",nextLoad.Configuration.Value.Modules[0].ModelVersion);
    }

    [Fact]
    public async Task PersistedFrozenAuditRestoresOldDescriptorAfterCurrentConfigurationChanges()
    {
        using var inputs = new ControlledCommissioningTests.Inputs();
        var path = Configure(inputs); var options = Station01OptionsReader.Read(Keys(inputs,path));
        var loader = new ConfigurationLoader(options.ConfigRoot,options.SchemaRoot);
        var original = RealAlgorithmConfigurationLoader.Load(path,Digest(path),options.SimulationReference,options.PublicReference,options.BudgetReference);
        var frozen = ConfigurationFreezer.Freeze(loader.LoadPublic(options.PublicReference),loader.LoadBudget(options.BudgetReference),null,
            new Dictionary<string,string>(),null,original.Configuration);
        var root = Path.Combine(inputs.Root,"022-frozen-audit-only");
        var dbOptions = Gaode.Infrastructure.Persistence.CameraCaptureJournal.Prepare(root);
        var run = Guid.NewGuid();
        await using(var db = new Gaode.Infrastructure.Persistence.Station01DbContext(dbOptions))
        { db.Runs.Add(new() {RunId=run,RequestId=run.ToString(),SubjectId="Test",ContextJson="{\"purpose\":\"Test\"}",Revision=1,CreatedUtc=DateTimeOffset.UtcNow}); await db.SaveChangesAsync(); }
        var json = JsonSerializer.Serialize(new {kind="FrozenPublicConfiguration",frozen.PublicJson,frozen.PublicDigest,
            frozen.BudgetJson,frozen.BudgetDigest,frozen.SimulationJson,frozen.SimulationDigest,
            frozen.RealAlgorithmJson,frozen.RealAlgorithmDigest,frozen.RealAlgorithmSourceFile,frozen.CapabilityVersions,frozen.SnapshotId},
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using(var writer = new Gaode.Infrastructure.Persistence.TraceWriter(dbOptions,TimeProvider.System,8))
        { var receipt = await writer.SubmitCritical(new(Guid.NewGuid(),run,1,WriteKind.Audit,json,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))))).Completion; Assert.Equal(CommitState.Committed,receipt.State); }
        var changed = JsonNode.Parse(File.ReadAllText(path))!; changed["version"]="2"; changed["modules"]![0]!["modelVersion"]="2";
        File.WriteAllText(path,changed.ToJsonString());
        var next = RealAlgorithmConfigurationLoader.Load(path,Digest(path),options.SimulationReference,options.PublicReference,options.BudgetReference);
        var persisted = Assert.Single(await new Gaode.Infrastructure.Persistence.TraceQuery(dbOptions).GetWritesAsync(run,default));
        using var audit = JsonDocument.Parse(persisted.PayloadJson);
        var restored = ConfigurationFreezer.RestoreAudit(audit.RootElement,"write://"+persisted.WriteId);
        Assert.Equal(frozen.SnapshotId,restored.SnapshotId); Assert.Equal(frozen.RealAlgorithmDigest,restored.RealAlgorithmDigest);
        Assert.Equal("1",restored.RealAlgorithm!.Modules[0].ModelVersion); Assert.Equal("2",next.Configuration.Value.Modules[0].ModelVersion);
        Assert.Equal(frozen.RealAlgorithmSourceFile,restored.RealAlgorithmSourceFile);
        var tampered = JsonNode.Parse(persisted.PayloadJson)!;tampered["realAlgorithmDigest"]=new string('0',64);
        using var invalid = JsonDocument.Parse(tampered.ToJsonString());
        Assert.Throws<InvalidOperationException>(() => ConfigurationFreezer.RestoreAudit(invalid.RootElement,"write://invalid"));
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }

    [Theory]
    [InlineData("missingPair")]
    [InlineData("digest")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("numericString")]
    [InlineData("capability")]
    [InlineData("noDescriptor")]
    public void ActualEntryRejectsInvalidConfigurationBeforeStorageOrDeviceStartup(string error)
    {
        using var inputs = new ControlledCommissioningTests.Inputs(); var path = Configure(inputs);
        var keys = Keys(inputs,path);
        if (error == "missingPair") keys["Gaode:RealAlgorithmConfigSha256"] = null;
        if (error == "digest") keys["Gaode:RealAlgorithmConfigSha256"] = new string('0',64);
        if (error is "unknown" or "numericString" or "capability")
        {
            var node = JsonNode.Parse(File.ReadAllText(path))!;
            if(error=="unknown") node["bypass"] = true;
            if(error=="numericString") node["modules"]![0]!["inputCount"]="1";
            if(error=="capability") node["modules"]![0]!["capabilityId"]="wrong";
            File.WriteAllText(path,node.ToJsonString()); keys["Gaode:RealAlgorithmConfigSha256"] = Digest(path);
        }
        if(error=="duplicate") { File.WriteAllText(path,File.ReadAllText(path).Replace("\"schemaVersion\":", "\"id\":\"duplicate\",\"schemaVersion\":")); keys["Gaode:RealAlgorithmConfigSha256"]=Digest(path); }
        if(error=="noDescriptor") { keys["Gaode:RealAlgorithmConfigPath"]=null; keys["Gaode:RealAlgorithmConfigSha256"]=null; }
        Assert.ThrowsAny<Exception>(() => new ServiceCollection().AddStation01(Station01OptionsReader.Read(keys)));
        Assert.False(Directory.Exists(inputs.Options.TestRoot));
    }
    private static string Configure(ControlledCommissioningTests.Inputs inputs)
    {
        var schemaRoot = Path.Combine(inputs.Root,"runtime-schema"); Directory.CreateDirectory(schemaRoot);
        foreach(var schemaFile in Directory.GetFiles(inputs.Options.SchemaRoot,"*.json")) File.Copy(schemaFile,Path.Combine(schemaRoot,Path.GetFileName(schemaFile)));
        File.Copy(Path.Combine(ControlledCommissioningTests.Inputs.RepoRoot(),"specs","021-commissioning-console","contracts","public-config.runtime.schema.json"),
            Path.Combine(schemaRoot,"public-config.schema.json"),true); // existing approved 021 runtime contract, isolated copy
        var p = JsonNode.Parse(File.ReadAllText(Path.Combine(inputs.Options.ConfigRoot,"public.json")))!;
        p["lightExecution"] = new JsonObject { ["schemaVersion"]="light-execution/1",["mode"]="Simulated" };
        foreach(var b in p["bindings"]!.AsArray()) if(b!["role"]!.GetValue<string>() is "TrayPose" or "FDecode") b["provider"]="Real";
        File.WriteAllText(Path.Combine(inputs.Options.ConfigRoot,"public.json"),p.ToJsonString());
        var file = new AlgorithmFileReference("not-delivered/model.bin","1",new string('a',64));
        AlgorithmModuleReference Module(string role, AlgorithmConfiguration binding,string result) => new(role,binding.BindingId!,binding.Capability!.Id,
            binding.Capability.ContractVersion,result,1,"provider-model","1",binding.ParametersVersion!,[file],file);
        var value = new RealAlgorithmConfiguration("real-algorithm-host/1","022-test-descriptor","1",RuntimePurposes.RealDeviceCommissioning,
            "Test:descriptor-validation-only;no-real-activation",new(inputs.Config.Id,inputs.Config.Version),inputs.Config.PublicConfigRef,inputs.Config.BudgetRef,
            inputs.Config.CodeRule,new("not-delivered-provider","1","Exe",file,"not-delivered",[],[]),
            [Module("TrayPose",inputs.Public.Algorithms.TrayPose!,"tray-observation/2"),Module("FDecode",inputs.Public.Algorithms.FDecode,"decoded-code/1")],
            new([8],["Gray"],null,null,new(null,null,null)),null,null);
        var path = Path.Combine(inputs.Root,"real-algorithm.json"); File.WriteAllText(path,JsonSerializer.Serialize(value,new JsonSerializerOptions(JsonSerializerDefaults.Web))); return path;
    }
    private static IConfigurationRoot Keys(ControlledCommissioningTests.Inputs inputs,string descriptor)
    {
        var o=inputs.Options; var c=o.Cameras!;
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Gaode:Mode"]=o.Mode,["Gaode:TestRoot"]=o.TestRoot,["Gaode:AllowedTestRoot"]=o.AllowedTestRoot,["Gaode:ConfigRoot"]=o.ConfigRoot,["Gaode:SchemaRoot"]=Path.Combine(inputs.Root,"runtime-schema"),
            ["Gaode:PublicId"]=o.PublicReference.Id,["Gaode:PublicVersion"]=o.PublicReference.Version,["Gaode:BudgetId"]=o.BudgetReference.Id,["Gaode:BudgetVersion"]=o.BudgetReference.Version,
            ["Gaode:CommissioningId"]=o.SimulationReference.Id,["Gaode:CommissioningVersion"]=o.SimulationReference.Version,
            ["Gaode:PlcHost"]=o.PlcHost,["Gaode:PlcPort"]=o.PlcPort.ToString(),["Gaode:PlcUnitId"]=o.PlcUnitId.ToString(),["Gaode:PlcProvider"]=o.PlcProvider,
            ["Gaode:PositionTolerance"]=o.PositionTolerance.ToString(System.Globalization.CultureInfo.InvariantCulture),["Gaode:PlcIoTimeoutMs"]=o.PlcIoTimeoutMs.ToString(),
            ["Gaode:PlcMechanicsPath"]=o.PlcMechanicsPath,["Gaode:PlcFieldProfilePath"]=o.PlcFieldProfilePath,
            ["Gaode:Cameras:Enabled"]="true",["Gaode:Cameras:SitePath"]=c.SitePath,["Gaode:Cameras:WorkerPath"]=c.WorkerPath,
            ["Gaode:Cameras:GalaxySdkPath"]=c.GalaxySdkPath,["Gaode:Cameras:CameraProSdkPath"]=c.CameraProSdkPath,["Gaode:Cameras:StateRoot"]=c.StateRoot,
            ["Gaode:RealAlgorithmConfigPath"]=descriptor,["Gaode:RealAlgorithmConfigSha256"]=Digest(descriptor) }).Build();
    }
    private static string Digest(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
