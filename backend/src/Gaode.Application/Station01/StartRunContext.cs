using System.Text.Json;
using System.Text.Json.Serialization;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed record ExpectedRecipeRef(string RecipeId, string Version, string CatalogDigest);

public sealed record StartRunContext(
    string SchemaVersion,
    Guid TrayId,
    string StationId,
    string LineId,
    string ScenarioId,
    IReadOnlyList<string> OccupiedSlots,
    RunPurpose Purpose, ExpectedRecipeRef? ExpectedRecipeRef = null)
{
    public const string CurrentSchemaVersion = "station01-start-run-context/1.0";
    public const string RecipeSchemaVersion = "station01-start-run-context/2.0";

    public WorkflowIdentity Freeze(Guid runId, string requestId, string actorId,
        DateTimeOffset startedAt, string publicConfigRevision, string budgetRevision,
        string simulationConfigRevision) =>
        new(runId, TrayId, StationId, LineId, requestId, ScenarioId,
            Array.AsReadOnly(OccupiedSlots.ToArray()), startedAt, actorId, Purpose,
            publicConfigRevision, budgetRevision, simulationConfigRevision);
}

public static class StartRunContextParser
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static StartRunContext Parse(string contextJson)
    {
        if (string.IsNullOrWhiteSpace(contextJson))
            throw new ArgumentException("StartRunContextRequired", nameof(contextJson));

        StartRunContextInput? input;
        try
        {
            input = JsonSerializer.Deserialize<StartRunContextInput>(contextJson, Options);
        }
        catch (JsonException error)
        {
            throw new ArgumentException("StartRunContextInvalidJson", nameof(contextJson), error);
        }

        if (input is null) throw new ArgumentException("StartRunContextRequired", nameof(contextJson));
        if (input.SchemaVersion is not (StartRunContext.CurrentSchemaVersion or StartRunContext.RecipeSchemaVersion))
            throw new ArgumentException("StartRunContextSchemaVersionUnsupported", nameof(contextJson));
        if (input.SchemaVersion == StartRunContext.RecipeSchemaVersion &&
            (string.IsNullOrWhiteSpace(input.ExpectedRecipeRef?.RecipeId) ||
             string.IsNullOrWhiteSpace(input.ExpectedRecipeRef?.Version) ||
             string.IsNullOrWhiteSpace(input.ExpectedRecipeRef?.CatalogDigest)))
            throw new ArgumentException("ExpectedRecipeRefRequired", nameof(contextJson));
        if (input.SchemaVersion == StartRunContext.CurrentSchemaVersion && input.ExpectedRecipeRef is not null)
            throw new ArgumentException("ExpectedRecipeRefRequiresContextV2", nameof(contextJson));
        if (input.TrayId == Guid.Empty)
            throw new ArgumentException("StartRunContextTrayIdRequired", nameof(contextJson));

        Require(input.StationId, "StationId", contextJson);
        Require(input.LineId, "LineId", contextJson);
        Require(input.ScenarioId, "ScenarioId", contextJson);
        if (input.OccupiedSlots is null)
            throw new ArgumentException("StartRunContextOccupiedSlotsRequired", nameof(contextJson));
        if (input.OccupiedSlots.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("StartRunContextOccupiedSlotInvalid", nameof(contextJson));

        var slots = input.OccupiedSlots.Select(value => value.Trim()).ToArray();
        if (slots.Distinct(StringComparer.Ordinal).Count() != slots.Length)
            throw new ArgumentException("StartRunContextOccupiedSlotsDuplicate", nameof(contextJson));
        if (!Enum.TryParse<RunPurpose>(input.Purpose, false, out var purpose))
            throw new ArgumentException("StartRunContextPurposeInvalid", nameof(contextJson));

        return new StartRunContext(input.SchemaVersion, input.TrayId,
            input.StationId!.Trim(), input.LineId!.Trim(), input.ScenarioId!.Trim(),
            Array.AsReadOnly(slots), purpose, input.ExpectedRecipeRef);
    }

    private static void Require(string? value, string field, string contextJson)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"StartRunContext{field}Required", nameof(contextJson));
    }

    private sealed record StartRunContextInput(
        string? SchemaVersion,
        Guid TrayId,
        string? StationId,
        string? LineId,
        string? ScenarioId,
        string[]? OccupiedSlots,
        string? Purpose, ExpectedRecipeRef? ExpectedRecipeRef);
}
