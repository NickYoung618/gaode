using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Gaode.Communication.Tests.ProtocolOracle;

public sealed class OracleSourceTests
{
    [Fact]
    public void RegisteredOracleIdentifiesTheConfirmedOriginal()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json")))
            root = root.Parent;
        Assert.NotNull(root);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "ProtocolOracle", "confirmed-20260925.json")));
        var source = json.RootElement.GetProperty("source");
        var document = Path.Combine(root.FullName, source.GetProperty("path").GetString()!);
        Assert.Equal(source.GetProperty("sha256").GetString(),
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(document))));
        // This checks provenance only. Production signal/codec comparisons belong to V-WIRE.
        Assert.Equal("plc-upper-20260925-partitioned-ack",
            json.RootElement.GetProperty("protocolIdentity").GetString());
    }
}
