using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Capabilities;
using Gaode.Domain.Configuration;
using Gaode.Host.Composition;
using Gaode.Infrastructure.Configuration;
using Gaode.Infrastructure.Devices.Cameras;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Simulation;
using Gaode.Infrastructure.Recipes;
using Gaode.Plc.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

var root = @"D:\Gaode-Station01\commissioning-021-final-3";
var configRoot = Path.Combine(root,"data/config/runtime-r1");
var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(configRoot,"hostsettings.json"),false)
    .AddJsonFile(Path.Combine(root,"data/private-identity/host-identities.json"),false).Build();
var g=config.GetSection("Gaode");
var options=new Station01RuntimeOptions(g["Mode"]!,g["TestRoot"]!,g["AllowedTestRoot"]!,g["ConfigRoot"]!,g["SchemaRoot"]!,
    new(g["PublicId"]!,g["PublicVersion"]!),new(g["BudgetId"]!,g["BudgetVersion"]!),new(g["CommissioningId"]!,g["CommissioningVersion"]!),
    g["PlcHost"]!,g.GetValue<int>("PlcPort"),g.GetValue<byte>("PlcUnitId"),g.GetValue<double>("PositionTolerance"),g["PlcProvider"]!,g.GetValue<int>("PlcIoTimeoutMs"),
    PlcMechanicsPath:g["PlcMechanicsPath"],PlcFieldProfilePath:g["PlcFieldProfilePath"],Cameras:RealCameraRegistration.ReadOptions(config),CommissioningPath:g["CommissioningPath"],CommissioningSha256:g["CommissioningSha256"]);
var loader=new ConfigurationLoader(options.ConfigRoot,options.SchemaRoot);
var pub=loader.LoadPublic(options.PublicReference); var budget=loader.LoadBudget(options.BudgetReference);
var input=CommissioningAlgorithmInputs.Load(options.CommissioningPath!,options.CommissioningSha256!);
var services=new ServiceCollection(); services.AddLogging(); services.AddStation01(options);
// Register services only: do not build/start WebApplication, hosted services or camera workers.
var identity=new Gaode.Host.Api.CommissioningIdentityRegistry(config);
var recipeOptions=config.GetSection("RecipeStore").Get<RecipeStoreOptions>()!;
recipeOptions.Validate(Path.Combine(options.TestRoot,"station01.test.db"));
if(!File.Exists(recipeOptions.DatabasePath)) throw new Exception("RecipeDatabaseMissing");
var plcOptions=new PlcRuntimeOptions { Provider=options.PlcProvider,Purpose=options.Mode,Host=options.PlcHost,Port=options.PlcPort,UnitId=options.PlcUnitId,IoTimeoutMs=options.PlcIoTimeoutMs,HeartbeatTimeoutMs=budget.Value.BusinessMs.HeartbeatDisconnect };
await using var device=new LatestProtocolPlcDevice(plcOptions,options.PositionTolerance,mechanicalConfigurationPath:options.PlcMechanicsPath,fieldProfilePath:options.PlcFieldProfilePath);
var violations=plcOptions.Definition!.Validate();
if(violations.Count!=0) throw new Exception("ProtocolDefinitionInvalid:"+string.Join(";",violations));
using var gateway=new GatewayScope(new PersistentCameraGateway(options.Cameras!));
foreach(var file in new[]{Path.Combine(options.Cameras!.GalaxySdkPath,"GxIAPI.dll"),Path.Combine(options.Cameras.CameraProSdkPath,"CameraPro.dll"),options.Cameras.WorkerPath,Path.Combine(root,"app/desktop/Gaode.Station01.Desktop.exe"),Path.Combine(root,"app/frontend/login.html")})
    if(!File.Exists(file)) throw new Exception("DependencyMissing:"+file);
var storeStatus=StoreCompatibilityProbe.Inspect(options.TestRoot,options.StoreProfile);
if(!storeStatus.Compatible) throw new Exception("RuntimeStore:"+storeStatus.Code);
using var store=new SqliteRecipeStore(recipeOptions,options.AllowedTestRoot,NullLogger<SqliteRecipeStore>.Instance,()=>"configuration-preflight");
var snapshot=store.GetSnapshot();
var recipe=snapshot.Definitions.Single(d=>d.RecipeId==input.Value.ExpectedRecipe.RecipeId);
var run=Guid.NewGuid();var tray=Guid.NewGuid();
var plan=RecipeRunPlanner.BuildExecutable(recipe,tray.ToString(),["s1"],options.Mode);
var limits=budget.Value.Limits;
var media=new MediaStore(Path.Combine(options.TestRoot,"media-root"),new MediaCapacity(limits.MediaMemoryBytes,limits.FReservedMemoryBytes,limits.RunMediaQuotaBytes,limits.DataQuotaBytes),new MediaLeaseRegistry(),limits.MediaJobs);
var algorithm=new CommissioningAlgorithm(input,media);
var capabilities=CapabilityRegistration.RegisterStation01(algorithm,options.Mode,input.Value);
var validation=new PublicConfigurationValidator(capabilities).Validate(pub.Value,budget.Value,null,false,false,"Real",true,input.Value);
if(!validation.CanStart || validation.AlgorithmIssues.Count!=0) throw new Exception("PublicValidation:"+string.Join(";",validation.BlockingControlErrors.Concat(validation.AlgorithmIssues)));
var realLightValidation=new PublicConfigurationValidator(capabilities).Validate(pub.Value with {LightExecution=new("light-execution/1","Real")},budget.Value,null,false,false,"Real",true,input.Value);
if(!realLightValidation.BlockingControlErrors.Contains("PublicLightParametersInvalid")) throw new Exception("RealLightMissingParametersNotBlocked");
var frozen=ConfigurationFreezer.Freeze(pub,budget,null,new Dictionary<string,string>(),input);
var cost=new ApprovedExecutionCostProvider().Resolve(frozen);
var execution=RecipeAdmission.Freeze(run,tray,plan,capabilities,cost,options.Mode,pub.Value.Algorithms.TrayPose);
algorithm.FreezeRun(run,tray,recipe.ScenarioId,pub.Value);
algorithm.BindRecipe(run,execution);
var pose=plan.Steps.Single(s=>s.Kind==RecipeStepKind.FlipMember).TargetPose!;
if(!plcOptions.PosePrograms.Any(p=>p.Model==recipe.Model && p.ProfileId==pose.ProfileId && p.ProfileVersion==pose.ProfileVersion && p.PoseKey==pose.PoseKey)) throw new Exception("FlipMappingMissing");
var data=new {schemaVersion="commissioning-runtime-preflight/1",passed=true,installedAssembliesUsed=true,formalHostRegistrationPassed=true,formalRecipeStoreOptionsPassed=true,formalIdentityRegistryPassed=true,formalPlcConstructionPassed=true,protocolViolations=violations.Count,sevenCameraBindings=gateway.Gateway.Status.Select(s=>new{s.Role,s.Serial,s.State}),nativeSdkFilesPresent=true,publicDigest=pub.Digest,budgetDigest=budget.Digest,inputDigest=input.Digest,recipeId=recipe.RecipeId,recipeVersion=recipe.Version,recipeDefinitionDigest=recipe.DefinitionDigest,formalSqliteReadbackPassed=true,formalAlgorithmFreezeAndRecipeBindPassed=true,formalRuntimeStoreCompatible=storeStatus.Compatible,publicAlgorithmIssues=validation.AlgorithmIssues,realLightMissingParametersBlocked=true,poseProgramCount=plcOptions.PosePrograms.Length,sortingConfigured=plcOptions.SortingSafePosition!=null,networkAccess=false,deviceDispatches=0,hostStarted=false,workersStarted=false,fieldValidated=false};
var json=JsonSerializer.Serialize(data,new JsonSerializerOptions{WriteIndented=true});
File.WriteAllText(Path.Combine(configRoot,"preflight-result.json"),json); Console.WriteLine(json);
sealed class GatewayScope(PersistentCameraGateway gateway):IDisposable
{ public PersistentCameraGateway Gateway=>gateway; public void Dispose()=>gateway.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
