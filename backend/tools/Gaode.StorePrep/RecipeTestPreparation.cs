using Gaode.Application.Recipes;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// Offline, explicit Test input preparation. Never registered as a Host save/catalog service.
internal static class RecipeTestPreparation
{
    public static object Seed(string allowedRoot, string root, string inputFile, string expectedSha256)
    {
        var input = new JsonRecipeCatalog(inputFile).GetSnapshot();
        if (!string.Equals(input.CatalogDigest, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("RecipeTestInputDigestMismatch");
        if (input.Definitions.Count == 0) throw new InvalidDataException("RecipeTestInputEmpty");
        using var ownership = StoreAccessGuard.Acquire(root, allowedRoot);
        var path = Path.Combine(ownership.Root, "recipes.db");
        RecipeStoreSchema.Verify(path, 10);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true,
            DefaultTimeout = 10, Pooling = false
        }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var db = new RecipeStoreDbContext(new DbContextOptionsBuilder<RecipeStoreDbContext>()
            .UseSqlite(connection, sqlite => sqlite.CommandTimeout(10)).Options);
        db.Database.UseTransaction(transaction);
        if (db.Heads.Any() || db.Contents.Any()) throw new InvalidOperationException("RecipeTestStoreMustBeEmpty");
        var definitions = new List<RecipeDefinition>();
        foreach (var source in input.Definitions)
        {
            var candidate = source with
            {
                RecipeId = RecipeDefinitionIdentity.CreateRecipeId(), Version = RecipeDefinitionIdentity.CreateVersion(),
                DefinitionDigest = "", CatalogDigest = ""
            };
            var validation = RecipeDefinitionValidator.ValidateForSave(candidate,
                RecipeCatalogSnapshots.Create(input.CatalogDigest, definitions));
            if (!validation.Valid) throw new InvalidDataException("RecipeTestInputInvalid:" +
                string.Join(",", validation.Issues.Select(issue => issue.Code + ":" + issue.FieldPath)));
            candidate = candidate with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(candidate) };
            var admission = RecipeAdmission.Evaluate(candidate, candidate.Positions.Select(position => position.SlotId), "Test");
            if (!admission.Eligible) throw new InvalidDataException("RecipeTestInputNotApproved:" + admission.Reason);
            definitions.Add(candidate);
        }
        var digest = SqliteRecipeStore.ComputeCatalogDigest(definitions);
        var requestId = Guid.NewGuid().ToString("N");
        foreach (var definition in definitions)
        {
            db.Contents.Add(new()
            {
                RecipeId = definition.RecipeId, Version = definition.Version,
                DefinitionDigest = definition.DefinitionDigest, ContractVersion = "recipe-contract/1.3",
                DefinitionJson = RecipeDefinitionSerialization.Serialize(definition with { CatalogDigest = digest }),
                SavedUtc = DateTimeOffset.UtcNow, ActorId = "StorePrep:explicit-Test-input",
                RequestId = requestId + ":" + input.CatalogDigest
            });
        }
        db.SaveChanges();
        db.Heads.AddRange(definitions.Select(definition => new RecipeHeadRow
        {
            RecipeId = definition.RecipeId, CurrentVersion = definition.Version, FCode = definition.FCode
        }));
        db.SaveChanges();
        transaction.Commit();
        return new { schema = "recipe-test-preparation/1", database = path, sourceSha256 = input.CatalogDigest,
            requestId, catalogDigest = digest, saved = definitions.Select(d => new { d.RecipeId, d.Version, d.FCode, d.DefinitionDigest }) };
    }
}
