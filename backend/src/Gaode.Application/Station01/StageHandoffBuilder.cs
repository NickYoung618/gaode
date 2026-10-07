using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Gaode.Application.Station01.Steps;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed record StageHandoff(Guid HandoffId, Guid RunId, Guid CommandId,
    string RequestId, string ContextJson, string Purpose, string PublicVersion,
    string BudgetVersion, string SimulationVersion, string SnapshotId,
    string ClockId, Guid ThreeDCaptureId, Guid HeightCallId,
    string ScopeId, string ScopeVersion, IReadOnlyList<HeightSample> HeightSamples,
    AlgorithmState HeightState, Guid ThreeDMediaId, Guid FCaptureId,
    Guid FCallId, Guid FMediaId, FCodeResult FCode, AlgorithmState FState,
    string ThreeDPointVersion, string FPointVersion,
    bool StartAccepted, bool PhysicalButton, bool ClampCompleted,
    bool ThreeDMoveCompleted, bool FMoveCompleted,
    string LastKnownDeviceState, string RecipeState, string QualityState,
    string SortingState, string WholeTaskState, string[] Limitations,
    TerminalOutcome Completion, string? RecipeRunPlanReference = null,
    string? PlanRevision = null, string? RecipeBindingReference = null);

public static class StageHandoffBuilder
{
    public static PublicPreparationHandoffV2 BuildV2(RunExecution run, StartPreparationEvidence start,
        ThreeDEvidence threeD, FEvidence f, string lastKnownDeviceState)
    {
        if (run.Identity is null || run.RecipePlan is null || string.IsNullOrWhiteSpace(run.PlanRevision) ||
            run.RecipeBindingId is null)
            throw new InvalidOperationException("HandoffV2PrerequisitesMissing");
        if (!start.Accepted || !start.DeviceReady || !start.Observation.HasReliableObservation ||
            start.Observation.Readiness != DeviceReadiness.Ready ||
            run.InitialObservation is not { IsValid: true } observation ||
            observation != threeD.Observation || run.InitialObservationWriteId is not { } observationWrite ||
            string.IsNullOrWhiteSpace(lastKnownDeviceState))
            throw new InvalidOperationException("CommittedInitialObservationRequired");
        var capabilityJson = JsonSerializer.Serialize(run.Config.CapabilityVersions
            .OrderBy(x => x.Key, StringComparer.Ordinal));
        var capabilityDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(capabilityJson)));
        var pointRevision = $"3d:{threeD.Move.PointVersion};f:{f.Move.PointVersion}";
        var fact = run.CommittedFSource;
        if (fact is null || fact.RunId != run.RunId || fact.CallId != f.Algorithm.CallId ||
            fact.CaptureId != f.Media.CaptureId || fact.CommittedRevision > run.PersistedRevision ||
            !fact.Origin.IsKnown || fact.Origin.Source is not { } source)
            throw new InvalidOperationException("CommittedFSourceMissingOrUnknown");
        var uniqueCode = f.Code.TrayIdentifier ?? f.Code.PrimaryCode
            ?? throw new InvalidOperationException("HandoffV2UniqueFCodeMissing");
        var recipeReference = $"recipe-plan://{run.RunId:D}/{run.PlanRevision}";
        var bindingReference = $"recipe-binding://{run.RecipeBindingId:D}";
        return new(PublicPreparationHandoffV2.CurrentSchemaVersion, Guid.NewGuid(),
            run.Identity, pointRevision, capabilityDigest,
            [$"media://{threeD.Media.MediaId:D}"], [$"media://{f.Media.MediaId:D}"],
            [$"algorithm-call://{observation.CallId:D}", $"algorithm-call://{f.Algorithm.CallId:D}"],
            uniqueCode, recipeReference, run.PlanRevision, bindingReference, source,
            "Verified",
            [$"write-revision://{run.RunId:D}/{run.PersistedRevision}",
                recipeReference, bindingReference, $"write://{fact.WriteId:D}", $"write://{observationWrite:D}"],
            Guid.Empty, 0, DateTimeOffset.MinValue, "");
    }
}
