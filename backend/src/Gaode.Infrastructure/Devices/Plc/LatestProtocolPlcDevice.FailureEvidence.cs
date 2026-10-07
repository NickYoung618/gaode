using Gaode.Domain.Station01;
using Gaode.Application.Ports;
using Gaode.Diagnostics;
using Gaode.Infrastructure.Persistence;
using Gaode.Plc.Protocol;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    private readonly List<Task> failureEvidenceTasks = [];
    private ActionCorrelation? evidenceCorrelation;
    private readonly Dictionary<Guid, TransitionEvidenceSegment> flipEvidenceSegments = [];

    // Only the current finite flip wait is segmented. A committed packet segment
    // never supplies a business completion or changes the caller's deadline.
    private async Task SaveTransitionSegmentAsync(ActionCorrelation correlation, ActionWindow window, CancellationToken token)
    {
        (long Business, long Heartbeat) start;
        TransitionEvidenceSegment? previous;
        lock (sync)
        {
            if (!evidenceStarts.TryGetValue(correlation.ActionId, out start))
                throw new InvalidOperationException("FlipEvidenceCaptureMissing");
            flipEvidenceSegments.TryGetValue(correlation.ActionId, out previous);
        }
        var next = await TransitionEvidenceCapture.CheckpointAsync(wire, heartbeat,
            evidenceRecorder ?? throw new InvalidOperationException("CommunicationEvidenceStoreUnavailable"),
            start, previous, correlation, () => Observe().Identity, Origin, window, options.Float32ByteOrder, token);
        if (next is not null)
            lock (sync) flipEvidenceSegments[correlation.ActionId] = next;
    }

    // Freeze actual buffered bytes at the failure boundary. The first latch wins;
    // subsequent repeated polls cannot fill the queue with the same failure.
    private void CaptureFailureEvidence(ActionCorrelation? correlation, ProtocolSample prior, string reason)
    {
        var recorder = evidenceRecorder;
        if (recorder is null)
        {
            RuntimeDiagnostics.Record("CommunicationEvidence", "FailureCaptureUnavailable", correlation?.RunId,
                new { correlation, reason, rawPersisted = false, authorizesAction = false });
            return;
        }
        (long Business, long Heartbeat) start = (Math.Max(0, wire.ExchangeSequence - 256), Math.Max(0, heartbeat.ExchangeSequence - 256));
        if (correlation is not null && evidenceStarts.TryGetValue(correlation.ActionId, out var admitted)) start = admitted;
        TransitionEvidenceSegment? preceding = null;
        if (correlation is not null && flipEvidenceSegments.TryGetValue(correlation.ActionId, out preceding))
            start = (preceding.Business, preceding.Heartbeat);
        IReadOnlyList<DiagnosticEvidenceReference>? precedingReferences = preceding?.References;
        if (correlation is not null && pickEvidenceSegments.TryGetValue(correlation.ActionId, out var picked))
        {
            start = (picked.Business, picked.Heartbeat);
            precedingReferences = picked.Evidence.DiagnosticEvidenceReferences;
        }
        var frozen = TransitionEvidenceCapture.Freeze(wire, heartbeat, start, precedingReferences);
        var exchanges = frozen.Exchanges;
        var observationId = prior.ObservationId == Guid.Empty ? Guid.NewGuid() : prior.ObservationId;
        var failedEpoch = correlation?.ConnectionEpoch ?? prior.ConnectionEpoch;
        var gap = frozen.Gap;
        var origin = Origin;
        failureEvidenceTasks.RemoveAll(task => task.IsCompleted);
        failureEvidenceTasks.Add(Task.Run(async () =>
        {
            try
            {
                var saved = await recorder.RecordFailureAsync(correlation, observationId, failedEpoch, origin,
                    exchanges, [], gap, options.Float32ByteOrder, reason, precedingReferences);
                RuntimeDiagnostics.Record("CommunicationEvidence", saved.Reference is null ? "FailureSaveUnconfirmed" : "FailureSaved",
                    correlation?.RunId, new { correlation, observationId, failedEpoch, reason, gap,
                        saved.WriteId, saved.ActualCommit, saved.Validity, saved.Reference, saved.FailureReason,
                        authorizesAction = false, currentSuccessReceipt = false });
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.Record("CommunicationEvidence", "FailureSaveUnconfirmed", correlation?.RunId,
                    new { correlation, observationId, failedEpoch, reason, gap, rawPersisted = false,
                        authorizesAction = false, newFactPersistenceGuaranteed = false }, error);
            }
        }));
    }
}

// Production evidence boundary shared by the device and the TCP/SQLite component.
// There is no force switch, alternative threshold, or completion authorization here.
internal sealed record TransitionEvidenceSegment(long Business, long Heartbeat,
    IReadOnlyList<DiagnosticEvidenceReference> References);

internal static class TransitionEvidenceCapture
{
    internal sealed record FailureWindow(IReadOnlyList<ModbusExchange> Exchanges, bool Gap,
        IReadOnlyList<DiagnosticEvidenceReference>? References);
    internal static FailureWindow Freeze(ModbusTcpClient businessWire, ModbusTcpClient heartbeatWire,
        (long Business, long Heartbeat) start, IReadOnlyList<DiagnosticEvidenceReference>? references)
    {
        var business = businessWire.EvidenceSince(start.Business);
        var pulse = heartbeatWire.EvidenceSince(start.Heartbeat);
        return new(business.Exchanges.Concat(pulse.Exchanges).OrderBy(x => x.StartedUtc).ToArray(),
            business.Gap || pulse.Gap, references);
    }
    internal static async Task<TransitionEvidenceSegment?> CheckpointAsync(ModbusTcpClient businessWire,
        ModbusTcpClient heartbeatWire, CommunicationEvidenceRecorder recorder,
        (long Business, long Heartbeat) admitted, TransitionEvidenceSegment? previous,
        ActionCorrelation correlation, Func<ObservationIdentity?> observe, ExecutionOrigin origin,
        ActionWindow window, Float32ByteOrder order, CancellationToken token)
    {
        var start = previous is null ? admitted : (previous.Business, previous.Heartbeat);
        if (businessWire.ExchangeSequence - start.Business < 1024 && heartbeatWire.ExchangeSequence - start.Heartbeat < 1024)
            return null;
        var business = businessWire.EvidenceSince(start.Business);
        var pulse = heartbeatWire.EvidenceSince(start.Heartbeat);
        var observation = observe();
        if (observation is not { IsValid: true, Reliability: DeviceReliability.Reliable } ||
            observation.ConnectionEpoch != correlation.ConnectionEpoch)
            throw new IOException("FlipCheckpointObservationUnavailable");
        var receipt = await recorder.RecordAsync(correlation, observation, origin, window,
            business.Exchanges.Concat(pulse.Exchanges).OrderBy(x => x.StartedUtc).ToArray(),
            business.Gap || pulse.Gap, order, "FlipCommunicationCheckpoint", token,
            precedingEvidence: previous?.References,
            captureScope: previous is null ? "FromActionAdmission" : "FlipContinuationAfterCommittedSegment");
        if (receipt.Reference is null || receipt.Validity != ReceiptValidity.ValidCurrent ||
            receipt.ActualCommit != ActualCommitState.Committed || token.IsCancellationRequested ||
            !window.Contains(System.Diagnostics.Stopwatch.GetTimestamp()))
            throw new CommunicationEvidenceUnavailableException(receipt);
        return new(business.LatestSequence, pulse.LatestSequence,
            (previous?.References ?? []).Append(receipt.Reference).ToArray());
    }
}
