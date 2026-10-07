using System.Security.Cryptography;
using System.Text.Json;

namespace Gaode.Infrastructure.Media;

public sealed record ControlledMediaFixture(string FixtureId, string RelativePath,
    string ContentType, string Sha256, bool Enabled, string Source, string Purpose);

public sealed class ControlledMediaFixtureIndex
{
    private readonly string _root;
    private readonly IReadOnlyDictionary<string, ControlledMediaFixture> _fixtures;

    public ControlledMediaFixtureIndex(string root, string manifestPath)
    {
        _root = Path.GetFullPath(root);
        if (!Directory.Exists(_root) || (File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("媒体fixture根未准备或为链接");
        var json = File.ReadAllText(manifestPath);
        var entries = JsonSerializer.Deserialize<IReadOnlyList<ControlledMediaFixture>>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        _fixtures = entries.ToDictionary(x => x.FixtureId, StringComparer.Ordinal);
        foreach (var fixture in _fixtures.Values) Validate(fixture);
    }

    public bool TryOpen(string fixtureId, out FileStream? stream, out ControlledMediaFixture? fixture)
    {
        stream = null;
        fixture = null;
        if (!_fixtures.TryGetValue(fixtureId, out var candidate) || !candidate.Enabled) return false;
        var path = Resolve(candidate.RelativePath);
        if (!File.Exists(path)) return false;
        var candidateStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.SequentialScan);
        var digest = Convert.ToHexString(SHA256.HashData(candidateStream));
        if (!string.Equals(digest, candidate.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            candidateStream.Dispose();
            return false;
        }
        candidateStream.Position = 0;
        fixture = candidate;
        stream = candidateStream;
        return true;
    }

    public static string ComputeSha256(Stream content) =>
        Convert.ToHexString(SHA256.HashData(content));

    private void Validate(ControlledMediaFixture fixture)
    {
        if (string.IsNullOrWhiteSpace(fixture.FixtureId) ||
            string.IsNullOrWhiteSpace(fixture.RelativePath) ||
            string.IsNullOrWhiteSpace(fixture.ContentType) ||
            string.IsNullOrWhiteSpace(fixture.Sha256) ||
            fixture.Source != "Simulated" || fixture.Purpose != "Test")
            throw new InvalidOperationException("媒体fixture manifest字段无效");
        _ = Resolve(fixture.RelativePath);
    }

    private string Resolve(string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath)) throw new InvalidOperationException("fixture不得使用绝对路径");
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("fixture路径越界");
        return full;
    }
}
