namespace Gaode.Domain.Diagnostics;

public sealed record StageError(string Code, string Category, string Message, string Stage,
    Guid? RunId, Guid? OperationId, string? ConfigVersion, string Disposition,
    string PersistenceState, DateTimeOffset OccurredUtc)
{
    public static StageError Create(string code, string category, string message, string stage,
        Guid? runId, Guid? operationId, string? version, string disposition,
        string persistence, TimeProvider clock) =>
        new(code, category, message, stage, runId, operationId, version, disposition,
            persistence, clock.GetUtcNow());
}
