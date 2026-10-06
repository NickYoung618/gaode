using System.Buffers.Binary;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Plc.Protocol;

namespace Gaode.Infrastructure.Devices.Plc;

// Composition service only. Its public surface exposes no raw values to Application/Domain.
public sealed class CommunicationEvidenceRecorder(TraceWriter writer, Guid storeId, TimeProvider clock, int criticalSaveBudgetMs)
{
    private readonly CommunicationEvidenceStore store = new(writer, clock);
    private readonly int failureSaveBudget = criticalSaveBudgetMs > 0 ? criticalSaveBudgetMs :
        throw new ArgumentOutOfRangeException(nameof(criticalSaveBudgetMs));
    internal Task<CommunicationEvidenceReceipt> RecordFailureAsync(ActionCorrelation? correlation,
        Guid observationId, long connectionEpoch, ExecutionOrigin origin, IReadOnlyList<ModbusExchange> exchanges,
        IReadOnlyList<RawHttpExchange> http, bool gap, Float32ByteOrder order, string reason,
        IReadOnlyList<DiagnosticEvidenceReference>? precedingEvidence = null)
    {
        var writeId = Guid.NewGuid();
        if (exchanges.Count == 0 && http.Count == 0)
            return Task.FromResult(new CommunicationEvidenceReceipt(writeId, ActualCommitState.Unknown,
                ReceiptValidity.None, null, null, clock.GetTimestamp(), "FailureCaptureUnavailable"));
        var records = exchanges.Select(ToRaw).ToArray();
        var now = clock.GetTimestamp(); var utc = clock.GetUtcNow();
        // This is the existing bounded failure-save obligation, never a new action window.
        var window = new ActionWindow(now, checked(now + (long)(failureSaveBudget * (double)clock.TimestampFrequency / 1000d)),
            "FailureSave/System", utc, utc.AddMilliseconds(failureSaveBudget));
        var batch = new CommunicationEvidenceBatch(Guid.NewGuid(), storeId, observationId,
            correlation?.RunId, correlation?.OperationId, correlation?.ActionId, connectionEpoch, origin,
            PlcAddressMap.Contract, order.ToString(), records.Select(e => e.StartedUtc).Concat(http.Select(e => e.StartedUtc)).Min(),
            records.Select(e => e.EndedUtc).Concat(http.Select(e => e.EndedUtc)).Max(), records,
            "FailureWindow:" + reason, gap, writeId)
            { HttpExchanges = http, CaptureScope = precedingEvidence is null ? "FailureWindowNoActionAuthorization" : "FailureWindowAfterCommittedSegments",
                PrecedingEvidenceReferences = precedingEvidence ?? [] };
        return store.SaveAsync(batch, window, CancellationToken.None);
    }
    internal Task<CommunicationEvidenceReceipt> RecordSimulationAsync(ActionCorrelation correlation,
        ObservationIdentity observation, ExecutionOrigin origin, ActionWindow window, string semanticFacts,
        CancellationToken token)
    {
        if (origin.Provider != DeviceProvider.Simulated || origin.ComponentVersion != "FullSimulation/semantic-009")
            throw new InvalidOperationException("SimulationEvidenceSourceInvalid");
        // A saved simulation observation has no TCP exchange. Never synthesize a packet or PLC connection.
        var batch = new CommunicationEvidenceBatch(Guid.NewGuid(), storeId, observation.ObservationId,
            correlation.RunId, correlation.OperationId, correlation.ActionId, correlation.ConnectionEpoch, origin,
            origin.ComponentVersion, "NotApplicable", observation.SampleStartedUtc, observation.SampleEndedUtc,
            [], semanticFacts, false, Guid.NewGuid());
        return store.SaveAsync(batch, window, token);
    }
    internal async Task<CommunicationEvidenceReceipt> RecordAsync(ActionCorrelation correlation,
        ObservationIdentity observation, ExecutionOrigin origin, ActionWindow window,
        IReadOnlyList<ModbusExchange> exchanges, bool gap, Float32ByteOrder order,
        string interpretation, CancellationToken cancellationToken, string? bindingId = null,
        IReadOnlyList<RawHttpExchange>? httpExchanges = null,
        IReadOnlyList<DiagnosticEvidenceReference>? precedingEvidence = null, string? captureScope = null)
    {
        var writeId = Guid.NewGuid();
        if (gap || exchanges.Count == 0)
            return new(writeId, ActualCommitState.Unknown, ReceiptValidity.None, null, null,
                clock.GetTimestamp(), gap ? "CommunicationEvidenceGap" : "CommunicationEvidenceMissing");
        var records = exchanges.Select(ToRaw).ToArray();
        var http = httpExchanges ?? [];
        var batch = new CommunicationEvidenceBatch(Guid.NewGuid(), storeId, observation.ObservationId,
            correlation.RunId, correlation.OperationId, correlation.ActionId, correlation.ConnectionEpoch, origin,
            PlcAddressMap.Contract, order.ToString(), records.Select(e => e.StartedUtc).Concat(http.Select(e => e.StartedUtc)).Min(),
            records.Select(e => e.EndedUtc).Concat(http.Select(e => e.EndedUtc)).Max(),
            records, interpretation, gap, writeId, bindingId)
        {
            HttpExchanges = http,
            CaptureScope = captureScope ?? (precedingEvidence is null ? "FromActionAdmission" : "CaptureReleaseSegmentAfterCommittedOpening"),
            PrecedingEvidenceReferences = precedingEvidence ?? []
        };
        var saveDue = Math.Min(window.DueTick, checked(clock.GetTimestamp() +
            (long)(failureSaveBudget * (double)clock.TimestampFrequency / 1000d)));
        return await store.SaveAsync(batch, window with { DueTick = saveDue,
            DeadlineUtc = clock.GetUtcNow().Add(clock.GetElapsedTime(clock.GetTimestamp(), saveDue)) }, cancellationToken);
    }
    private static RawExchange ToRaw(ModbusExchange exchange)
    {
        var request = exchange.Request.Length == 0 ? [] : Convert.FromHexString(exchange.Request);
        int? offset = request.Length >= 10 ? BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(8, 2)) : null;
        byte? function = request.Length >= 8 ? request[7] : null;
        int? count = function is 1 or 3 or 16 && request.Length >= 12
            ? BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(10, 2)) : function is 5 or 6 ? 1 : null;
        return new(exchange.ConnectionId, exchange.Channel,
            request.Length >= 2 ? BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(0, 2)) : null,
            request.Length >= 7 ? request[6] : null, function, offset, count,
            exchange.StartedUtc, exchange.ObservedAtUtc, request.Length > 0 ? exchange.Request : null,
            exchange.Response, exchange.Error);
    }
}
