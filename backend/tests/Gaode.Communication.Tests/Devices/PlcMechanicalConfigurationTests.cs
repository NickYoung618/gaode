using System.Text.Json;
using Gaode.Infrastructure.Devices.Plc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed class PlcMechanicalConfigurationTests
{
    [Theory]
    [InlineData("Virtual", true)]
    [InlineData("Real", false)]
    public async Task ExplicitTestMechanicalInputIsLoadedOnlyForItsDeclaredPurpose(string provider, bool permitted)
    {
        var workspace = new DirectoryInfo(AppContext.BaseDirectory);
        while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "global.json"))) workspace = workspace.Parent;
        var evidence = Path.Combine(workspace?.FullName ?? throw new InvalidOperationException("WorkspaceMissing"),
            "artifacts", "011-plc-interaction-update", "mechanics-input");
        Directory.CreateDirectory(evidence);
        var path = Path.Combine(evidence, Guid.NewGuid().ToString("N") + ".json");
        var source = new PlcMechanicalConfiguration
        {
            SchemaVersion = "plc-mechanics/1", Purpose = "Test", SourceReference = "Component:explicit-input",
            PosePrograms = [new("model", "motion", "test/1", "pose-b", 23, [101, 102], "Component:payload", "Test")],
            SortingSafePosition = new(150, "mm", "SIM_MACHINE", "Test", "Component:safety-position")
        };
        File.WriteAllText(path, JsonSerializer.Serialize(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        try
        {
            var options = new PlcRuntimeOptions { Provider = provider };
            if (!permitted)
            {
                Assert.Throws<InvalidDataException>(() => new LatestProtocolPlcDevice(options, .01, mechanicalConfigurationPath: path));
                Assert.Empty(options.PosePrograms);
                Assert.Null(options.SortingSafePosition);
                return;
            }
            await using var device = new LatestProtocolPlcDevice(options, .01, mechanicalConfigurationPath: path);
            var program = Assert.Single(options.PosePrograms);
            Assert.Equal(source.PosePrograms[0].ModelWords, program.ModelWords);
            Assert.Equal((ushort)23, program.TargetFaceWord);
            Assert.Equal(source.SortingSafePosition, options.SortingSafePosition);
        }
        finally { File.Delete(path); }
    }
}
