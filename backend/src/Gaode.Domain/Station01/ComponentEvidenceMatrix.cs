namespace Gaode.Domain.Station01;

public enum ComponentKind { Host, Plc, Camera, Light, Algorithm, ManualActor }
public enum EvidenceMilestone { ReadyForUnlock, FinalUnloadCompletion, ReadyForRemoval }
public enum ComponentEvidenceState { Verified, NotYetRequired, Missing, Unknown, Unverifiable }
public enum ComponentEvidenceSource { Real, Virtual, Simulated, Test, AuthenticatedHuman }
public enum EvidenceScope { SoftwareLoopOnly, ProductionCandidateNotAccepted }

// Producer identity, not an execution-success claim. A completed evidence record
// must also carry actual correlated references before it can be Verified.
public sealed record ComponentExecutionOrigin(ComponentEvidenceSource? Source, string? VersionRef, string? Quality)
{
    public static ComponentExecutionOrigin Unknown { get; } = new(null, null, null);
    public bool IsKnown => Source is not null && !string.IsNullOrWhiteSpace(VersionRef) &&
        !string.IsNullOrWhiteSpace(Quality) && Quality != "Unknown";
}

public sealed record ComponentEvidence(
    ComponentKind Component,
    ComponentEvidenceState State,
    ComponentEvidenceSource? Source,
    string? Quality,
    string? VersionRef,
    IReadOnlyList<string> EvidenceReferences,
    DateTimeOffset? CapturedAt,
    string? Digest)
{
    public static ComponentEvidence NotYetRequired(ComponentKind component) =>
        new(component, ComponentEvidenceState.NotYetRequired, null, null, null, [], null, null);

    public bool IsVerifiable => State == ComponentEvidenceState.Verified &&
        Source is not null && !string.IsNullOrWhiteSpace(Quality) &&
        !string.Equals(Quality, "Unknown", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(VersionRef) && EvidenceReferences.Count > 0 &&
        EvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x)) &&
        CapturedAt is not null && !string.IsNullOrWhiteSpace(Digest);
}

public sealed class ComponentEvidenceMatrix
{
    private static readonly ComponentKind[] AllComponents = Enum.GetValues<ComponentKind>();
    private readonly IReadOnlyList<ComponentEvidence> components;
    private readonly IReadOnlyList<ComponentKind> blockedComponents;

    private ComponentEvidenceMatrix(
        Guid matrixId,
        Guid runId,
        Guid trayId,
        string planRevision,
        EvidenceMilestone milestone,
        IReadOnlyList<ComponentEvidence> components,
        IReadOnlyList<ComponentKind> blockedComponents,
        EvidenceScope scope,
        bool containsNonProductionEvidence)
    {
        MatrixId = matrixId;
        RunId = runId;
        TrayId = trayId;
        PlanRevision = planRevision;
        Milestone = milestone;
        this.components = components;
        this.blockedComponents = blockedComponents;
        Scope = scope;
        ContainsNonProductionEvidence = containsNonProductionEvidence;
    }

    public Guid MatrixId { get; }
    public Guid RunId { get; }
    public Guid TrayId { get; }
    public string PlanRevision { get; }
    public EvidenceMilestone Milestone { get; }
    public IReadOnlyList<ComponentEvidence> Components => components;
    public IReadOnlyList<ComponentKind> BlockedComponents => blockedComponents;
    public EvidenceScope Scope { get; }
    public bool ContainsNonProductionEvidence { get; }
    public bool IsComplete => blockedComponents.Count == 0;

    public static ComponentEvidenceMatrix Create(
        Guid matrixId,
        Guid runId,
        Guid trayId,
        string planRevision,
        EvidenceMilestone milestone,
        IEnumerable<ComponentEvidence> evidence)
    {
        if (matrixId == Guid.Empty || runId == Guid.Empty || trayId == Guid.Empty)
            throw new ArgumentException("Matrix, run and tray identities are required.");
        if (string.IsNullOrWhiteSpace(planRevision))
            throw new ArgumentException("Plan revision is required.", nameof(planRevision));

        var items = evidence?.ToArray() ?? throw new ArgumentNullException(nameof(evidence));
        var duplicates = items.GroupBy(x => x.Component).Where(x => x.Count() != 1)
            .Select(x => x.Key).ToArray();
        if (duplicates.Length > 0)
            throw new ArgumentException($"Duplicate component evidence: {string.Join(',', duplicates)}", nameof(evidence));

        var byComponent = items.ToDictionary(x => x.Component);
        var normalized = AllComponents.Select(component => byComponent.TryGetValue(component, out var item)
            ? item with { EvidenceReferences = item.EvidenceReferences.ToArray() }
            : new ComponentEvidence(component, ComponentEvidenceState.Missing, null, null, null, [], null, null))
            .ToArray();

        var required = milestone is EvidenceMilestone.ReadyForUnlock or EvidenceMilestone.ReadyForRemoval
            ? AllComponents.Where(x => x != ComponentKind.ManualActor)
            : AllComponents;
        var blocked = required.Where(component => !byComponent.TryGetValue(component, out var item) || !item.IsVerifiable)
            .ToArray();

        if (milestone is EvidenceMilestone.ReadyForUnlock or EvidenceMilestone.ReadyForRemoval &&
            byComponent.TryGetValue(ComponentKind.ManualActor, out var manual) &&
            manual.State != ComponentEvidenceState.NotYetRequired)
            blocked = [.. blocked, ComponentKind.ManualActor];

        var containsNonProduction = normalized.Any(x => x.Source is
            ComponentEvidenceSource.Virtual or ComponentEvidenceSource.Simulated or ComponentEvidenceSource.Test);
        var scope = containsNonProduction
            ? EvidenceScope.SoftwareLoopOnly
            : EvidenceScope.ProductionCandidateNotAccepted;

        return new ComponentEvidenceMatrix(matrixId, runId, trayId, planRevision, milestone,
            Array.AsReadOnly(normalized), Array.AsReadOnly(blocked.Distinct().ToArray()), scope,
            containsNonProduction);
    }
}
