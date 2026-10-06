namespace Gaode.Application.Recipes;

public static class RecipeMatcher
{
    public static RecipeMatchResult Match(RecipeCatalogSnapshot snapshot, string trayCode,
        RecipeSelectionIntent? selection, string scenarioId, string purpose)
    {
        if (snapshot.SchemaVersion != RecipeCatalogSnapshot.CurrentSchema)
            throw new InvalidDataException("RecipeCatalogSchemaUnsupported");
        var matches = snapshot.Definitions.Where(r => string.Equals(r.FCode, trayCode, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0) return new(RecipeMatchStatus.Unmatched, null, snapshot.CatalogDigest, "RecipeTrayCodeUnmatched");
        if (matches.Length != 1) return new(RecipeMatchStatus.Ambiguous, null, snapshot.CatalogDigest, "RecipeTrayCodeAmbiguous");
        var definition = RecipeCatalogSnapshots.Freeze(matches[0]) with { CatalogDigest = snapshot.CatalogDigest };
        if (selection is not null && selection.RecipeId != definition.RecipeId)
            return new(RecipeMatchStatus.IdentityMismatch, definition, snapshot.CatalogDigest, "RecipeSelectionIdentityMismatch");
        string? problem = definition.ScenarioId != scenarioId ? "RecipeScenarioMismatch" : null;
        if (problem is null && (string.IsNullOrWhiteSpace(definition.RecipeId) || string.IsNullOrWhiteSpace(definition.Version) ||
            definition.DefinitionDigest != RecipeDefinitionIdentity.ComputeDefinitionDigest(definition))) problem = "RecipeSavedIdentityInvalid";
        if (problem is null)
        {
            try { problem = RecipeAdmission.Evaluate(definition, [], purpose).Reason; }
            catch (InvalidDataException error) { problem = error.Message; }
        }
        return new(problem is null ? RecipeMatchStatus.Matched : RecipeMatchStatus.Restricted,
            definition, snapshot.CatalogDigest, problem);
    }
}
