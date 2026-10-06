namespace Gaode.Domain.Station01;

public readonly record struct RunId(Guid Value)
{
    public static RunId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public readonly record struct OperationId(Guid Value)
{
    public static OperationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("D");
}

public enum RunPurpose
{
    Production,
    Test,
    Commissioning
}

public sealed record WorkflowIdentity(
    Guid RunId,
    Guid TrayId,
    string StationId,
    string LineId,
    string RequestId,
    string ScenarioId,
    IReadOnlyList<string> OccupiedSlots,
    DateTimeOffset StartedAt,
    string ActorId,
    RunPurpose Purpose,
    string PublicConfigRevision,
    string BudgetRevision,
    string SimulationConfigRevision)
{
    public void EnsureScope(Guid runId, Guid trayId)
    {
        if (RunId != runId || TrayId != trayId)
            throw new InvalidOperationException("WorkflowIdentityMismatch");
    }
}
