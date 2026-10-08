using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Simulation;

public sealed class CommissioningAlgorithm : IAlgorithmPort, IAlgorithmCapabilityProvider, ICommissioningRunInputs
{
    private sealed record RunInputs(Guid TrayId, PublicConfiguration Public, FrozenExecutionInputs? Recipe, CommissioningConfiguration Inputs);
    private readonly CommissioningConfiguration config;
    private readonly string digest;
    private readonly MediaStore media;
    private readonly ConcurrentDictionary<Guid, RunInputs> runs = new();
    private readonly ConcurrentDictionary<AlgorithmRole, int> counts = new();
    private readonly Guid session = Guid.NewGuid();
    public CommissioningAlgorithm(LoadedConfiguration<CommissioningConfiguration> loaded, MediaStore media)
    {
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(loaded.CanonicalJson))), loaded.Digest,
            StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("CommissioningInputDigestMismatch");
        config = JsonSerializer.Deserialize<CommissioningConfiguration>(loaded.CanonicalJson, CommissioningAlgorithmInputs.Json)!;
        CommissioningAlgorithmInputs.ValidateIdentity(config);
        digest = loaded.Digest; this.media = media;
    }
    public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Simulated, $"{config.Id}/{config.Version}:{digest}", "ControlledCommissioningAlgorithm/1");
    public string ImplementationReference => "ControlledCommissioningAlgorithm/1";
    public IReadOnlyList<AlgorithmCapabilityDeclaration> AlgorithmCapabilities => config.Algorithms.ToArray();
    public int CallCount(AlgorithmRole role) => counts.GetValueOrDefault(role);
    public void FreezeRun(Guid runId, Guid trayId, string scenarioId, PublicConfiguration configuration, Gaode.Application.Station01.ExpectedRecipeRef? selection = null, FLocation? fLocation = null)
    {
        try { FreezeRunCore(runId, trayId, scenarioId, configuration, selection, fLocation); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            Log("RunInputsRejectedBeforeMotion", runId, null, new { trayId, scenarioId, error.Message }, error);
            throw;
        }
    }
    private void FreezeRunCore(Guid runId, Guid trayId, string scenarioId, PublicConfiguration configuration,
        Gaode.Application.Station01.ExpectedRecipeRef? selection, FLocation? fLocation)
    {
        var config = SelectInputs(selection, scenarioId);
        if (fLocation is not null) config = config with { FLocation = fLocation };
        if (runId == Guid.Empty || trayId == Guid.Empty || scenarioId != config.ExpectedRecipe.ScenarioId ||
            configuration.Purpose != config.Purpose || config.PublicConfigRef != new ConfigReference(configuration.Id, configuration.Version) ||
            config.RawCodes.Count != 1 || config.RawCodes[0] != config.ExpectedRecipe.FCode || config.FLocation is not { IsValid: true } location ||
            location.Frame != configuration.Motion.Frame || location.X < configuration.Motion.Limits.XMin || location.X > configuration.Motion.Limits.XMax ||
            location.Y < configuration.Motion.Limits.YMin || location.Y > configuration.Motion.Limits.YMax ||
            string.IsNullOrWhiteSpace(config.MappingSourceReference) || config.Slots.Count == 0 ||
            config.Slots.Any(s => !s.HasPositionIdentity || s.PhysicalSlotIndex <= 0 || s.Presence == TrayPresence.Unknown || s.Presence == TrayPresence.Present && s.Pose != TrayPose.Normal) ||
            config.Slots.Select(s => s.PhysicalSlotIndex).Distinct().Count() != config.Slots.Count ||
            config.Slots.Select(s => s.CellId).Distinct().Count() != config.Slots.Count ||
            !MatchesPublic(AlgorithmPurpose.TrayCode, configuration.Algorithms.FDecode) ||
            !MatchesPublic(AlgorithmPurpose.TrayPose, configuration.Algorithms.TrayPose) ||
            (configuration.LightExecution?.IsSimulated != true &&
                (!config.PublicLightChannels.ContainsKey(configuration.Capture3d.LightBindingId) ||
                 !config.PublicLightChannels.ContainsKey(configuration.CaptureF.LightBindingId))))
            throw new InvalidOperationException("CommissioningSafeInputsMissingOrScopeMismatch");
        var copy = JsonSerializer.Deserialize<PublicConfiguration>(JsonSerializer.Serialize(configuration), new JsonSerializerOptions())!;
        if (!runs.TryAdd(runId, new(trayId, copy, null, config))) throw new InvalidOperationException("CommissioningRunAlreadyFrozen");
        Log("RunInputsFrozen", runId, null, new { trayId, scenarioId, config.ExpectedRecipe });
    }
    public void BindRecipe(Guid runId, FrozenExecutionInputs inputs)
    {
        try { BindRecipeCore(runId, inputs); }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            Log("RecipeInputsRejectedBeforeDependentMotion", runId, null, new { inputs.Plan.RecipeId, error.Message }, error);
            throw;
        }
    }
    private bool MatchesPublic(AlgorithmPurpose purpose, AlgorithmConfiguration? configured) =>
        configured?.Capability is { } capability && config.Algorithms.Any(a => a.Purpose == purpose &&
            a.CapabilityId == capability.Id && a.CapabilityVersion == capability.ContractVersion &&
            a.ParametersVersion == configured.ParametersVersion);
    private void BindRecipeCore(Guid runId, FrozenExecutionInputs inputs)
    {
        if (!runs.TryGetValue(runId, out var run)) throw new InvalidOperationException("CommissioningRunInputsNotFrozen");
        var config = run.Inputs;
        var expected = config.ExpectedRecipe;
        if (run.Recipe is not null || inputs.RunId != runId || inputs.TrayId != run.TrayId ||
            inputs.Plan.RecipeId != expected.RecipeId || inputs.Plan.RecipeVersion != expected.Version || inputs.Plan.DefinitionDigest != expected.DefinitionDigest ||
            inputs.Plan.Model != expected.Model || inputs.Plan.FCode != expected.FCode || inputs.Plan.ScenarioId != expected.ScenarioId)
            throw new InvalidOperationException("CommissioningExpectedRecipeMismatch");
        foreach (var step in inputs.Plan.Steps.Where(s => s.Kind == RecipeStepKind.Capture))
            FindResult(config, AlgorithmPurpose.SingleDetection, Scope(step, step.Camera!));
        foreach (var group in inputs.Plan.Steps.Where(s => s.Kind == RecipeStepKind.Capture)
            .GroupBy(s => (s.SlotId, s.Material, s.StageId, s.LocalFace, s.CoordinateEpoch)))
            FindResult(config, AlgorithmPurpose.FaceFusion, Scope(group.First(), string.Concat(group.Select(s => s.Camera).Order(StringComparer.Ordinal))));
        foreach (var step in inputs.Plan.Steps.Where(s => s.Kind == RecipeStepKind.ReadECode))
            FindEntityCode(config, Scope(step, "E"));
        if (!runs.TryUpdate(runId, run with { Recipe = inputs }, run)) throw new InvalidOperationException("CommissioningBindingChanged");
        Log("RecipeInputsFrozen", runId, null, new { expected.RecipeId, expected.Version, expected.DefinitionDigest, inputs.PlanRevision });
    }
    private CommissioningConfiguration SelectInputs(Gaode.Application.Station01.ExpectedRecipeRef? selection, string scenario)
    {
        var candidates = new List<CommissioningConfiguration> { config };
        foreach (var input in config.RecipeInputs ?? [])
            candidates.Add(config with { Id = input.Id, Version = input.Version, Source = input.Source,
                ExpectedRecipe = input.ExpectedRecipe, Slots = input.Slots, FLocation = input.FLocation,
                MappingSourceReference = input.MappingSourceReference, RawCodes = input.RawCodes,
                Results = input.Results, EntityCodes = input.EntityCodes, RecipeInputs = null });
        var matches = candidates.Where(c => c.ExpectedRecipe.ScenarioId == scenario &&
            (selection is null ? candidates.Count == 1 : c.ExpectedRecipe.RecipeId == selection.RecipeId &&
             c.ExpectedRecipe.Version == selection.Version)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("CommissioningRecipeInputsMissingOrAmbiguous");
        return matches[0];
    }
    private static CommissioningEntityCodeResult FindEntityCode(CommissioningConfiguration config, CommissioningResultScope scope)
    {
        var matches = (config.EntityCodes ?? []).Where(c => c.Scope == scope).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("CommissioningEntityCodeInputsMissingOrAmbiguous");
        return matches[0];
    }
    private static IReadOnlyList<string> ResolveEntityCodes(RunInputs run, AlgorithmRequest request)
    {
        var plan = run.Recipe?.Plan ?? throw new InvalidOperationException("CommissioningRecipeInputsNotFrozen");
        var identity = request.TargetIdentity ?? throw new InvalidOperationException("CommissioningTargetIdentityMissing");
        var step = plan.Steps.Single(s => s.Kind == RecipeStepKind.ReadECode && (s.MemberId ?? s.UnitId) == identity.ObjectId &&
            s.StageId == identity.StageId && s.LocalFace == identity.LocalFace && s.CoordinateEpoch == identity.HeightRound);
        return FindEntityCode(run.Inputs, Scope(step, "E")).Codes;
    }
    public void ReleaseRun(Guid runId) => runs.TryRemove(runId, out _);
    private static CommissioningResultScope Scope(RecipeStep step, string camera) =>
        new(step.SlotId!, step.Material!, step.StageId!, step.LocalFace!.Value, step.CoordinateEpoch, camera);
    private static CommissioningAlgorithmResult FindResult(CommissioningConfiguration config, AlgorithmPurpose purpose, CommissioningResultScope scope)
    {
        var results = config.Results.Where(r => r.Purpose == purpose && r.Scope == scope).ToArray();
        if (results.Length != 1) throw new InvalidOperationException("CommissioningDetectionInputMissingOrAmbiguous");
        return results[0];
    }
    public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request, Action<AlgorithmEvent> onEvent, CancellationToken ct)
    {
        try
        {
            if (!request.Envelope.IsValid || request.Envelope.Purpose != config.Purpose || request.IntentWriteId == Guid.Empty ||
                request.CallId == Guid.Empty || request.Inputs.Select(m => m.MediaId).Distinct().Count() != request.Inputs.Count ||
                !runs.TryGetValue(request.Envelope.RunId, out var run))
                throw new InvalidOperationException("CommissioningRunInputsNotFrozen");
            var purpose = request.Role switch
            {
                AlgorithmRole.FDecode => AlgorithmPurpose.TrayCode, AlgorithmRole.EDecode => AlgorithmPurpose.EntityCode, AlgorithmRole.TrayPose => AlgorithmPurpose.TrayPose,
                AlgorithmRole.Detection when request.Inputs.Count == 1 => AlgorithmPurpose.SingleDetection,
                AlgorithmRole.Detection when request.Inputs.Count == 2 => AlgorithmPurpose.FaceFusion,
                _ => throw new InvalidOperationException("CommissioningAlgorithmRoleUnsupported")
            };
            var binding = config.Algorithms.SingleOrDefault(b => b.Purpose == purpose);
            if (binding is null || binding.InputCount != request.Inputs.Count || binding.CapabilityId != request.CapabilityId ||
                binding.CapabilityVersion != request.CapabilityVersion || binding.ParametersVersion != request.ParametersVersion)
                throw new InvalidOperationException("CommissioningAlgorithmBindingMismatch");
            string? disposition = null;
            if (request.Role == AlgorithmRole.Detection)
            {
                var plan = run.Recipe?.Plan ?? throw new InvalidOperationException("CommissioningRecipeInputsNotFrozen");
                var identities = request.InputIdentities ?? (request.TargetIdentity is { } target ? [target] : []);
                if (identities.Count != request.Inputs.Count) throw new InvalidOperationException("CommissioningTargetIdentityMissing");
                var steps = identities.Select(identity => plan.Steps.Single(s => s.Kind == RecipeStepKind.Capture &&
                    (s.MemberId ?? s.UnitId) == identity.ObjectId && s.StageId == identity.StageId && s.LocalFace == identity.LocalFace &&
                    s.CoordinateEpoch == identity.HeightRound && s.Camera == identity.Camera)).ToArray();
                var scope = Scope(steps[0], string.Concat(steps.Select(s => s.Camera).Order(StringComparer.Ordinal)));
                if (steps.Any(s => Scope(s, scope.Camera) != scope)) throw new InvalidOperationException("CommissioningFusionScopeMismatch");
                disposition = FindResult(run.Inputs, purpose, scope).Disposition;
            }
            if (request.Role == AlgorithmRole.TrayPose && (request.ObservationContext is not { } observation || observation.TrayId != run.TrayId))
                throw new InvalidOperationException("CommissioningObservationContextMismatch");
            if (request.Role == AlgorithmRole.EDecode) ResolveEntityCodes(run, request);
            counts.AddOrUpdate(request.Role, 1, (_, count) => count + 1);
            onEvent(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: session));
            Log("Accepted", request.Envelope.RunId, request, new { purpose, binding });
            var exited = ExecuteAsync(request, run, disposition, onEvent, ct);
            return ValueTask.FromResult(new AlgorithmDispatch(exited));
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            Log("RejectedBeforeDispatch", request.Envelope.RunId, request, new { error.Message }, error);
            throw new AlgorithmNotDispatchedException(request.CallId, error.Message);
        }
    }
    private async Task ExecuteAsync(AlgorithmRequest request, RunInputs run, string? disposition, Action<AlgorithmEvent> onEvent, CancellationToken ct)
    {
        var config = run.Inputs;
        var leases = new List<IDisposable>();
        await Task.Yield();
        try
        {
            onEvent(new(request, AlgorithmEventKind.Running, WorkerSessionId: session));
            var expectedKind = request.Role switch { AlgorithmRole.TrayPose => "PointCloud", AlgorithmRole.FDecode => "Image", AlgorithmRole.EDecode => "EImage", _ => "DetectionImage" };
            foreach (var input in request.Inputs)
            {
                if (!media.TryGetReference(input.MediaId, out var actual) || actual.RunId != request.Envelope.RunId ||
                    actual.CaptureId != input.CaptureId || actual.RelativeKey != input.RelativeKey || actual.ByteLength != input.ByteLength ||
                    actual.Format != input.Format || actual.Kind != expectedKind || input.Kind != expectedKind ||
                    actual.StorageState != "FileCompleted" || !media.IsReady(input.MediaId) ||
                    !request.Inputs.Any(m => m.CaptureId == request.CaptureId))
                    throw new InvalidDataException("CommissioningMediaIdentityMismatch");
                leases.Add(media.Lease(input.MediaId, $"commissioning:{request.CallId}"));
                await using var stream = await media.OpenReadAsync(input.MediaId, ct);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
                Log("CurrentMediaRead", request.Envelope.RunId, request, new { input.MediaId, input.CaptureId, input.ByteLength, hash });
            }
            ct.ThrowIfCancellationRequested();
            TrayObservation? observed = null;
            if (request.Role == AlgorithmRole.TrayPose)
            {
                var context = request.ObservationContext!;
                observed = new(Guid.NewGuid(), request.Envelope.RunId, run.TrayId, request.CaptureId, request.CallId,
                    DateTimeOffset.UtcNow, context.Purpose, context.CheckRound, context.RelatedTransitionId, config.Slots,
                    context.Purpose == TrayObservationPurpose.InitialPreparation ? config.FLocation : null,
                    Origin, [$"controlled-input:{config.Id}/{config.Version}:{digest}", config.Source])
                    { SchemaVersion = "tray-observation/2", MappingSourceReference = config.MappingSourceReference,
                        ExpectedPhysicalSlotIndices = config.Slots.Select(s => s.PhysicalSlotIndex).ToArray() };
                if (!observed.HasCompleteCoverage) throw new InvalidDataException("CommissioningObservationInvalid");
            }
            var result = new AlgorithmEvent(request, AlgorithmEventKind.Result,
                RawCodes: request.Role == AlgorithmRole.FDecode ? config.RawCodes : request.Role == AlgorithmRole.EDecode ? ResolveEntityCodes(run, request) : null,
                WorkerSessionId: session, DetectionDisposition: disposition) { Observation = observed };
            Log("ResultSimulated", request.Envelope.RunId, request, new { disposition, observed?.ObservationId, Origin });
            onEvent(result);
        }
        catch (Exception error)
        {
            Log("Failed", request.Envelope.RunId, request, new { error.Message, disposition = "NoDependentMotion" }, error);
            onEvent(new(request, AlgorithmEventKind.Failed, ErrorCode: error.Message, WorkerSessionId: session));
        }
        finally
        {
            foreach (var lease in leases) lease.Dispose();
            onEvent(new(request, AlgorithmEventKind.InputReleased, WorkerSessionId: session));
            onEvent(new(request, AlgorithmEventKind.WorkerExited, WorkerSessionId: session));
            Log("InputsReleasedAndExecutionEnded", request.Envelope.RunId, request, new { session });
        }
    }
    private void Log(string stage, Guid run, AlgorithmRequest? request, object detail, Exception? error = null) =>
        RuntimeDiagnostics.Record("CommissioningAlgorithm", stage, run, new { request?.CallId, request?.CaptureId,
            OperationId = request?.Envelope.OperationId, request?.CapabilityId, request?.ParametersVersion,
            inputDigest = digest, Origin, detail }, error);
}
