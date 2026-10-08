using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;
using Gaode.Application.Recipes;

namespace Gaode.Infrastructure.Algorithms;

/// <summary>Serializes Test calls over the one Host-owned worker process.</summary>
public sealed class PythonWorkerAdapter : IAlgorithmPort, IAlgorithmCapabilityProvider
{
    private readonly WorkerProcessSupervisor? supervisor;
    private readonly SemaphoreSlim calls = new(1, 1);
    private readonly ConcurrentDictionary<AlgorithmRole, int> counts = new();
    private int unresolvedExecution;
    public ComponentExecutionOrigin Origin => supervisor is null ? ComponentExecutionOrigin.Unknown :
        new(ComponentEvidenceSource.Test, supervisor.Implementation?.Name ?? "PythonWorkerAdapter/1", "IndependentTestWorker");
    public string ImplementationReference => supervisor?.Implementation?.Reference ?? GetType().FullName!;
    public IReadOnlyList<AlgorithmCapabilityDeclaration> AlgorithmCapabilities { get; } = [
        new(AlgorithmPurpose.SingleDetection, "detection.single", "1.0", "image-quality/1", 1, "worker-request"),
        new(AlgorithmPurpose.FaceFusion, "detection.fusion", "1.0", "face-quality/1", 2, "worker-request"),
        new(AlgorithmPurpose.EntityCode, "code.raw-candidates", "1.0", "decoded-code/1", 1, "worker-request"),
        new(AlgorithmPurpose.TrayPose, "tray.observation", "1.0", "tray-observation/2", 1, "worker-request") ];
    public bool InputsAndExecutionsReleased => calls.CurrentCount == 1 &&
        (Volatile.Read(ref unresolvedExecution) == 0 || supervisor?.HasExited == true);
    public PythonWorkerAdapter(WorkerProcessSupervisor? supervisor = null) => this.supervisor = supervisor;
    public int CallCount(AlgorithmRole role) => counts.GetValueOrDefault(role);
    public async ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
        Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
    {
        if (request.CallId == Guid.Empty || !request.Envelope.IsValid ||
            request.Inputs.Count is < 1 or > 2 ||
            (request.InputIdentities is not null &&
                request.InputIdentities.Count != request.Inputs.Count) ||
            request.Inputs.Any(input => input.StorageState != "FileCompleted" ||
                input.RunId != request.Envelope.RunId) ||
            request.Inputs.Select(input => input.MediaId).Distinct().Count() != request.Inputs.Count ||
            (request.Inputs.Count == 1 && request.Inputs[0].CaptureId != request.CaptureId) ||
            (request.Inputs.Count == 2 && (request.Role != AlgorithmRole.Detection ||
                !request.Inputs.Any(input => input.CaptureId == request.CaptureId))))
            throw new AlgorithmNotDispatchedException(request.CallId, "InvalidWorkerInput");
        if (request.Role is not (AlgorithmRole.TrayPose or AlgorithmRole.FDecode or AlgorithmRole.EDecode or AlgorithmRole.Detection))
            throw new AlgorithmNotDispatchedException(request.CallId, "UnsupportedWorkerCapability");
        if (supervisor is null || supervisor.HasExited)
            throw new AlgorithmNotDispatchedException(request.CallId, "WorkerUnavailable");
        RuntimeDiagnostics.Record("WorkerDispatch", "WaitingForSlot", request.Envelope.RunId,
            new { request.CallId, request.Envelope.OperationId, request.Envelope.Attempt,
                workerSessionId = supervisor.SessionId, role = request.Role.ToString() });
        await calls.WaitAsync(cancellationToken);
        if (Volatile.Read(ref unresolvedExecution) != 0)
        {
            calls.Release();
            throw new AlgorithmNotDispatchedException(request.CallId, "PreviousWorkerExecutionUnconfirmed");
        }
        var leaseId = Guid.NewGuid().ToString("N");
        WorkerMediaInput[] inputs;
        try
        {
            inputs = request.Inputs.Select((media, index) => new WorkerMediaInput(
                Guid.NewGuid().ToString("N"), media.MediaId, media.CaptureId,
                media.RelativeKey, media.ContentType, media.ByteLength,
                supervisor.DigestInput(media.RelativeKey),
                (request.InputIdentities?[index] ?? request.TargetIdentity)?.ObjectId,
                (request.InputIdentities?[index] ?? request.TargetIdentity)?.LocalFace,
                (request.InputIdentities?[index] ?? request.TargetIdentity)?.HeightRound,
                (request.InputIdentities?[index] ?? request.TargetIdentity)?.Camera)
                { StageId = (request.InputIdentities?[index] ?? request.TargetIdentity)?.StageId }).ToArray();
            RuntimeDiagnostics.Record("WorkerDispatch", "Sending", request.Envelope.RunId,
                new { request.CallId, request.Envelope.OperationId, workerSessionId = supervisor.SessionId,
                    leaseId, inputs = inputs.Select(input => new { input.MediaId, input.CaptureId,
                        input.Sha256, input.ObjectId, input.LocalFace, input.HeightRound, input.Camera, input.StageId }) });
            Volatile.Write(ref unresolvedExecution, 1);
            await supervisor.SendAsync(WorkerProtocolCodec.Execute(supervisor.SessionId,
                new WorkerExecutionInput(request.CallId, request.Envelope.Attempt,
                    request.Role.ToString(), leaseId, inputs,
                    request.ParametersVersion, request.CapabilityId,
                    request.CapabilityVersion) { ObservationContext = request.ObservationContext }), cancellationToken);
        }
        catch (Exception error)
        {
            supervisor.RecordFailure(request.Envelope.RunId, request.CallId, "SendFailed", error);
            calls.Release();
            throw;
        }
        counts.AddOrUpdate(request.Role, 1, (_, count) => count + 1);
        return new AlgorithmDispatch(ReadResultAsync());

        async Task ReadResultAsync()
        {
            var accepted = false;
            var responseCount = 0;
            var released = new HashSet<int>();
            try
            {
                while (true)
                {
                    var line = await supervisor.ReadLineAsync();
                    if (line is null) throw new IOException("WorkerExited");
                    var message = WorkerProtocolCodec.Decode(Encoding.UTF8.GetBytes(line));
                    if (++responseCount <= 16)
                        RuntimeDiagnostics.Record("WorkerFeedback", "Received", request.Envelope.RunId,
                            new { request.CallId, request.Envelope.OperationId, workerSessionId = supervisor.SessionId,
                                message.Type, message.ErrorCode, actualCallId = message.CallId,
                                actualSessionId = message.WorkerSessionId, message.Attempt,
                                message.Role, message.LeaseId }, warning: message.ErrorCode is not null);
                    if (message.WorkerSessionId != supervisor.SessionId ||
                        message.CallId != request.CallId ||
                        message.Attempt != request.Envelope.Attempt ||
                        message.Role != request.Role.ToString() || message.LeaseId != leaseId)
                        throw new InvalidDataException("WorkerCorrelationMismatch");
                    if (message.Type == "Accepted")
                    {
                        accepted = true;
                        onEvent(new(request, AlgorithmEventKind.Accepted,
                            WorkerSessionId: supervisor.SessionId));
                    }
                    else if (message.Type == "Result")
                    {
                        if (!accepted) throw new InvalidDataException("WorkerResultBeforeAccepted");
                        if (message.ErrorCode is not null)
                            onEvent(new(request, AlgorithmEventKind.Failed,
                                ErrorCode: message.ErrorCode, WorkerSessionId: supervisor.SessionId));
                        else
                        {
                            using var result = JsonDocument.Parse(message.ResultJson!);
                            IReadOnlyList<string>? codes = null;
                            string? disposition = null;
                            TrayObservation? observation = null;
                            if (request.Role == AlgorithmRole.TrayPose)
                                observation = ReadObservation(result.RootElement, request);
                            else if (request.Role is AlgorithmRole.FDecode or AlgorithmRole.EDecode)
                                codes = result.RootElement.GetProperty("rawCodes")
                                    .EnumerateArray().Select(x => x.GetString()!).ToArray();
                            else
                                disposition = result.RootElement.GetProperty("disposition").GetString();
                            onEvent(new(request, AlgorithmEventKind.Result, RawCodes: codes,
                                WorkerSessionId: supervisor.SessionId,
                                DetectionDisposition: disposition) { Observation = observation });
                        }
                    }
                    else if (message.Type == "InputReleased")
                    {
                        var index = message.InputIndex!.Value;
                        if (index < 0 || index >= inputs.Length || !released.Add(index) ||
                            message.Reason != inputs[index].Sha256)
                            throw new InvalidDataException("WorkerInputReleaseMismatch");
                        if (released.Count == inputs.Length)
                        {
                            Volatile.Write(ref unresolvedExecution, 0);
                            onEvent(new(request, AlgorithmEventKind.InputReleased,
                                WorkerSessionId: supervisor.SessionId));
                            return;
                        }
                    }
                    else throw new InvalidDataException("WorkerUnexpectedMessage");
                }
            }
            catch (Exception error)
            {
                supervisor.RecordFailure(request.Envelope.RunId, request.CallId, "ReadOrDecodeFailed", error);
                if (supervisor.HasExited)
                    onEvent(new(request, AlgorithmEventKind.WorkerExited, WorkerSessionId: supervisor.SessionId));
                throw;
            }
            finally { calls.Release(); }
        }
    }
    private TrayObservation ReadObservation(JsonElement result, AlgorithmRequest request)
    {
        var context = request.ObservationContext ?? throw new InvalidDataException("TrayObservationContextMissing");
        var body = result.GetProperty("trayObservation");
        T EnumValue<T>(string name, JsonElement value) where T : struct, Enum =>
            Enum.TryParse<T>(value.GetProperty(name).GetString(), false, out var parsed) && Enum.IsDefined(parsed)
                ? parsed : throw new InvalidDataException("TrayObservation" + name + "Invalid");
        var purpose = EnumValue<TrayObservationPurpose>("purpose", body);
        var round = body.GetProperty("checkRound").GetInt32();
        var transition = body.GetProperty("relatedTransitionId").ValueKind == JsonValueKind.Null ? (Guid?)null : body.GetProperty("relatedTransitionId").GetGuid();
        if (purpose != context.Purpose || round != context.CheckRound || transition != context.RelatedTransitionId)
            throw new InvalidDataException("TrayObservationContextMismatch");
        var slots = body.GetProperty("slots").EnumerateArray().Select(slot => new TraySlotObservation(
            slot.GetProperty("physicalSlotIndex").GetInt32(), EnumValue<TrayPresence>("presence", slot), EnumValue<TrayPose>("pose", slot),
            slot.GetProperty("reason").GetString()) { CellId = slot.GetProperty("cellId").GetString(),
                Region = slot.GetProperty("region").GetString(), Row = slot.GetProperty("row").GetInt32(),
                Column = slot.GetProperty("column").GetInt32() }).ToArray();
        FLocation? location = null;
        if (body.GetProperty("fLocation") is { ValueKind: JsonValueKind.Object } f)
            location = new(f.GetProperty("x").GetDouble(), f.GetProperty("y").GetDouble(), f.GetProperty("unit").GetString()!,
                f.GetProperty("frame").GetString()!, f.GetProperty("sourceReference").GetString()!);
        var observation = new TrayObservation(body.GetProperty("observationId").GetGuid(), request.Envelope.RunId, context.TrayId,
            request.CaptureId, request.CallId, body.GetProperty("observedAtUtc").GetDateTimeOffset(), purpose, round, transition,
            Array.AsReadOnly(slots), location, Origin, Array.AsReadOnly(request.Inputs.Select(m => $"media://{m.MediaId:D}").ToArray())) {
                SchemaVersion = body.GetProperty("schemaVersion").GetString()!,
                MappingSourceReference = body.GetProperty("mappingSourceReference").GetString(),
                ExpectedPhysicalSlotIndices = Array.AsReadOnly(body.GetProperty("expectedPhysicalSlotIndices").EnumerateArray().Select(i => i.GetInt32()).ToArray()) };
        if (!observation.HasCompleteCoverage) throw new InvalidDataException("TrayObservationInvalidOrIncomplete");
        return observation;
    }}
