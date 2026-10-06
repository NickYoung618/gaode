using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Recipes;

namespace Gaode.Infrastructure.Recipes;

// An explicit alternative input adapter, not an active Host catalog or approval source.
// Complete RC08 bodies and independently identified fixed coordinates enter common validation.
public sealed class SemanticRecipeInputProvider : IRecipeCatalog
{
    public const string InputSchema = "semantic-recipe-input/2";
    public const string CoordinateColumns = "slot,objectPattern,index,face,camera,stageId,pointRef,pointId,version,x,y,z,unit,frame,configurationVersion,sourceFactReference,approvalReference";
    private readonly string[] bodies;
    private readonly string digest;

    public SemanticRecipeInputProvider(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != InputSchema)
            throw new InvalidDataException("SemanticInputSchemaUnsupported");
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var definitions = new List<string>();
        var sourceDigests = new List<string> { Convert.ToHexString(SHA256.HashData(bytes)) };
        foreach (var entry in root.GetProperty("definitions").EnumerateArray())
        {
            var definition = RecipeEnvironmentDecoder.Decode(entry.GetProperty("definition"));
            var coordinatePath = Path.GetFullPath(Path.Combine(directory, entry.GetProperty("coordinatesFile").GetString()!));
            if (!coordinatePath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("CoordinateSourceOutsideInputDirectory");
            var coordinateBytes = File.ReadAllBytes(coordinatePath);
            var coordinateDigest = Convert.ToHexString(SHA256.HashData(coordinateBytes));
            if (coordinateDigest != entry.GetProperty("coordinatesSha256").GetString())
                throw new InvalidDataException("CoordinateSourceDigestMismatch");
            sourceDigests.Add(coordinateDigest);
            using var reader = new StringReader(System.Text.Encoding.UTF8.GetString(coordinateBytes));
            if (reader.ReadLine() != CoordinateColumns) throw new InvalidDataException("CoordinateColumnsInvalid");
            var targets = new List<CoordinateDefinition>();
            while (reader.ReadLine() is { } line)
            {
                var c = line.Split(',');
                if (c.Length != 17 || c.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException("CoordinateRowInvalid");
                double Number(int index) => double.Parse(c[index], CultureInfo.InvariantCulture);
                targets.Add(new(c[6], new(c[7], c[8], Number(9), Number(10), c[12], c[13]),
                    c[1], c[0], int.Parse(c[2], CultureInfo.InvariantCulture), int.Parse(c[3], CultureInfo.InvariantCulture),
                    c[4], c[5], c[14], c[15], new(Number(11), c[12], c[13], c[16], c[14])));
            }
            // Bind by supplied slot and configured object identity, never list index or face-derived stage.
            var positions = definition.Positions.ToDictionary(p => p.SlotId, StringComparer.Ordinal);
            var consumed = new HashSet<CoordinateDefinition>();
            IReadOnlyList<CoordinateDefinition> Coordinates(string slot, string pattern)
            {
                var items = targets.Where(t => t.SlotId == slot && t.ObjectPattern == pattern).ToArray();
                foreach (var item in items) consumed.Add(item);
                return items;
            }
            var execution = definition.ExecutionPositions.ToDictionary(p => p.Key, p =>
            {
                var position = positions[p.Key];
                var physicalPattern = definition.UnitKind == "independentPart" && position.Members.Count == 1
                    ? position.Members[0].MemberPattern : position.UnitPattern;
                return p.Value with
                {
                    PhysicalEntity = p.Value.PhysicalEntity with { Coordinates = Coordinates(p.Key, physicalPattern) },
                    Members = p.Value.Members.ToDictionary(m => m.Key, m => m.Value with
                    {
                        Coordinates = Coordinates(p.Key, position.Members.Single(member => member.Material == m.Key).MemberPattern)
                    }, StringComparer.Ordinal)
                };
            }, StringComparer.Ordinal);
            if (targets.Any(t => !consumed.Contains(t))) throw new InvalidDataException("CoordinateObjectUnregistered");
            // Approval/recipe identity are preserved. The common save operation forms saved identity.
            definitions.Add(RecipeDefinitionSerialization.Serialize(definition with { ExecutionPositions = execution }));
        }
        bodies = definitions.ToArray();
        digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", sourceDigests))));
    }

    public RecipeCatalogSnapshot GetSnapshot() => RecipeCatalogSnapshots.Create(digest,
        bodies.Select(RecipeDefinitionSerialization.Deserialize).ToArray());
}
