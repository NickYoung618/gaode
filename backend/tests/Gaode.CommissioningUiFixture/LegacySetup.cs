using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Host.Composition;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Communication.Tests;
using Gaode.Communication.Tests.Devices;

internal sealed partial class OfflineFixture
{
    private async Task PrepareLegacyAsync()
    {
        recipe = Recipe011Data.ForSlots(2, 1);
        recipe = RecipeDefinitionSerialization.Deserialize(RecipeDefinitionSerialization.Serialize(recipe).Replace("test-frame", "SIM_MACHINE", StringComparison.Ordinal));
        recipe = recipe with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe) };
        var configRoot = inputs.Options.ConfigRoot;
        var p = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "public.json")))!;
        p["purpose"] = "Test"; p["source"] = "OFFLINE:legacy-Test-normal-chain";
        p["motion"]!["axes"] = new JsonArray("X", "Y", "Z"); p["motion"]!["capability"]!["id"] = "xyz.fixed";
        p["motion"]!["frame"] = "SIM_MACHINE"; p["motion"]!["positionTolerance"] = .01;
        foreach (var point in p["motion"]!["points"]!.AsObject().Select(x => x.Value).OfType<JsonNode>()) point["frame"] = "SIM_MACHINE";
        p["motion"]!["points"]!["unload"] = JsonSerializer.SerializeToNode(new FixedPoint("OFFLINE-unload", "1", 1, 2, "mm", "SIM_MACHINE", 3), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        p["parser"] = JsonSerializer.SerializeToNode(new ParserConfiguration(new("code.test-tray-format", "1.0"), "1"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var b in p["bindings"]!.AsArray()) b!["provider"] = b["role"]!.GetValue<string>() == "PLC" ? "Virtual" : "Simulated";
        File.WriteAllText(Path.Combine(configRoot, "public.json"), p.ToJsonString());
        var budget = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "budget.json")))!;
        budget["purpose"] = "Test"; budget.AsObject().Remove("recipeExecution");
        budget["businessMs"]!["trayPoseAlgorithm"] = 1000;
        budget["businessMs"]!["flipCompletion"] = 1000;
        budget["businessMs"]!["putBackCompletion"] = 1000;
        File.WriteAllText(Path.Combine(configRoot, "budget.json"), budget.ToJsonString());
        var simulation = ReviewBusinessData.Read<SimulationProfile>("simulation.normal.json");
        File.WriteAllText(Path.Combine(configRoot, "simulation.json"), JsonSerializer.Serialize(simulation, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        legacyPlc = new ProtocolTcpFixture(400);
        var plc = legacyPlc;
        var timeout = lifetime;
        await plc.StartAsync(timeout.Token);
        device = plc.Device(o => {
            o.SortingSafePosition = new(9, "mm", "SIM_MACHINE", "Test", "OFFLINE:explicit-safe-Z");
            o.PosePrograms = [new(recipe.Model, "motion", "test-1", "face-2", 2, new ushort[16], "OFFLINE:legacy-Test-pose", "Test")];
        });
        await HandshakeClosureTests.Ready(device, timeout.Token);
        options = inputs.Options with { Mode = "VirtualPlcIntegration", PlcProvider = "Virtual", PlcPort = plc.Port,
            SimulationReference = new(simulation.Id, simulation.Version), CommissioningPath = null, CommissioningSha256 = null,
            PlcMechanicsPath = null, PlcFieldProfilePath = null, Cameras = null };
    }
}
