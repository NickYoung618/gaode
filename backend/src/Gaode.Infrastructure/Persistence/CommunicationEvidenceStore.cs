using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gaode.Infrastructure.Persistence;

// Infrastructure-only captured traffic. No business port returns this record or its reader.
internal sealed record RawExchange(Guid ConnectionId, string Channel, ushort? TransactionId,
    byte? Unit, byte? Function, int? Offset, int? Count, DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc, string? RequestHex, string? ResponseHex, string? Error);
// HTTP bodies are captured separately; they have no Modbus transaction or known socket identity.
internal sealed record RawHttpExchange(Guid ExchangeId, string Method, string Endpoint,
    DateTimeOffset StartedUtc, DateTimeOffset EndedUtc, string? RequestBodyHex,
    string? ResponseBodyHex, int? StatusCode, string? Error);
internal sealed record CommunicationEvidenceBatch(Guid EvidenceId, Guid StoreId, Guid ObservationId,
    Guid? RunId, Guid? OperationId, Guid? ActionId, long ConnectionEpoch, ExecutionOrigin Origin,
    string InterpretationVersion, string ByteOrder, DateTimeOffset ObservedStartUtc,
    DateTimeOffset ObservedEndUtc, IReadOnlyList<RawExchange> Exchanges, string Interpretation,
    bool Gap, Guid WriteId, string? BindingId = null)
{
    public IReadOnlyList<RawHttpExchange> HttpExchanges { get; init; } = [];
    public string CaptureScope { get; init; } = "FromActionAdmission";
    public IReadOnlyList<DiagnosticEvidenceReference> PrecedingEvidenceReferences { get; init; } = [];
    public string SchemaVersion => "plc-evidence/1";
    public bool IsValid => EvidenceId != Guid.Empty && StoreId != Guid.Empty && ObservationId != Guid.Empty &&
        WriteId != Guid.Empty && ConnectionEpoch > 0 && ObservedEndUtc >= ObservedStartUtc &&
        Origin.Provider != DeviceProvider.Unavailable && !string.IsNullOrWhiteSpace(InterpretationVersion) &&
        !string.IsNullOrWhiteSpace(ByteOrder) && !string.IsNullOrWhiteSpace(Interpretation) &&
        (CaptureScope is "FromActionAdmission" or "FailureWindowNoActionAuthorization" && PrecedingEvidenceReferences.Count == 0 ||
            CaptureScope is "CaptureReleaseSegmentAfterCommittedOpening" or "FlipContinuationAfterCommittedSegment" or "PlaceContinuationAfterCommittedPick" or
                "FailureWindowAfterCommittedSegments" && PrecedingEvidenceReferences.Count > 0 &&
            PrecedingEvidenceReferences.All(r => r.IsValid && r.StoreId == StoreId)) &&
        (Origin.Provider == DeviceProvider.Simulated ? Exchanges.Count == 0 && HttpExchanges.Count == 0 && ByteOrder == "NotApplicable" &&
            InterpretationVersion == "FullSimulation/semantic-009" : Exchanges.Count + HttpExchanges.Count > 0) &&
        Exchanges.All(e => e.ConnectionId != Guid.Empty &&
            e.EndedUtc >= e.StartedUtc && !string.IsNullOrWhiteSpace(e.Channel) &&
            (e.RequestHex is not null || e.Error is not null) &&
            (e.ResponseHex is not null || e.Error is not null)) &&
        HttpExchanges.All(e => e.ExchangeId != Guid.Empty && e.EndedUtc >= e.StartedUtc &&
            e.Method is "GET" or "POST" && Uri.TryCreate(e.Endpoint, UriKind.Absolute, out _) &&
            (e.ResponseBodyHex is not null && e.StatusCode is >= 100 and <= 599 || e.Error is not null));
}
internal sealed record CommunicationEvidenceReceipt(Guid WriteId, ActualCommitState ActualCommit,
    ReceiptValidity Validity, DiagnosticEvidenceReference? Reference, DateTimeOffset? CommittedUtc,
    long ReceivedTick, string? FailureReason);
internal sealed record EvidenceCommitResult(ActualCommitState ActualCommit, DateTimeOffset? CommittedUtc,
    string? FailureReason);

internal sealed class CommunicationEvidenceStore(TraceWriter writer, TimeProvider clock)
{
    public async Task<CommunicationEvidenceReceipt> SaveAsync(CommunicationEvidenceBatch batch,
        ActionWindow window, CancellationToken cancellationToken)
    {
        if (!batch.IsValid || !window.IsValid) throw new ArgumentException("CommunicationEvidenceIdentityInvalid");
        if (!window.Contains(clock.GetTimestamp()) || cancellationToken.IsCancellationRequested)
            return Unconfirmed(batch.WriteId, "EvidenceWindowClosedBeforeQueue");
        Task<EvidenceCommitResult> pending;
        try { pending = writer.SubmitCommunication(batch, window, cancellationToken); }
        catch (InvalidOperationException error) { return Unconfirmed(batch.WriteId, error.Message); }
        try
        {
            var remaining = clock.GetElapsedTime(clock.GetTimestamp(), window.DueTick);
            if (remaining <= TimeSpan.Zero) return Unconfirmed(batch.WriteId, "EvidenceWindowExpired");
            var actual = await pending.WaitAsync(remaining, clock, cancellationToken);
            var received = clock.GetTimestamp();
            var valid = actual.ActualCommit == ActualCommitState.Committed &&
                actual.FailureReason is null && !cancellationToken.IsCancellationRequested && window.Contains(received);
            return new(batch.WriteId, actual.ActualCommit, valid ? ReceiptValidity.ValidCurrent : ReceiptValidity.Invalid,
                valid ? new(batch.StoreId, batch.EvidenceId) : null, actual.CommittedUtc, received, actual.FailureReason ?? (valid ? null : "EvidenceReceiptExpired"));
        }
        catch (Exception error) when (error is TimeoutException or OperationCanceledException)
        { return Unconfirmed(batch.WriteId, error is TimeoutException ? "EvidenceReceiptTimeout" : "EvidenceReceiptCancelled"); }
    }
    private CommunicationEvidenceReceipt Unconfirmed(Guid writeId, string reason) =>
        new(writeId, ActualCommitState.Unknown, ReceiptValidity.None, null, null, clock.GetTimestamp(), reason);
}

public sealed partial class TraceWriter
{
    private readonly ConcurrentDictionary<Guid, CommunicationPending> _communicationPending = new();
    // Finite Test seams surround the real SQLite transaction; they never replace CommitCommunication.
    internal Func<CommunicationEvidenceBatch, CancellationToken, Task>? BeforeCommunicationCommit { get; set; }
    internal Func<CommunicationEvidenceBatch, EvidenceCommitResult, CancellationToken, Task>? AfterCommunicationCommitBeforeReceipt { get; set; }

    internal Task<EvidenceCommitResult> SubmitCommunication(CommunicationEvidenceBatch batch,
        ActionWindow window, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(batch, JsonOptions);
        // Freeze caller-owned collections before enqueue; the immutable bytes are the persistence identity.
        var frozen = JsonSerializer.Deserialize<CommunicationEvidenceBatch>(json, JsonOptions)!;
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var item = new CommunicationPending(frozen, json, digest, window, cancellationToken);
        if (!_communicationPending.TryAdd(batch.EvidenceId, item)) throw new InvalidOperationException("EvidenceWriteAlreadyPending");
        if (!_queue.Writer.TryWrite(item))
        { _communicationPending.TryRemove(batch.EvidenceId, out _); throw new InvalidOperationException("EvidenceQueueFull"); }
        return item.Done.Task;
    }

    private async Task ConsumeCommunicationAsync(CommunicationPending pending)
    {
        EvidenceCommitResult result = new(ActualCommitState.Unknown, null, "EvidenceNotCommitted");
        try
        {
            if (pending.Cancellation.IsCancellationRequested || !pending.Window.Contains(_clock.GetTimestamp()))
            { pending.Done.TrySetResult(result with { FailureReason = "EvidenceWindowClosedBeforeCommit" }); return; }
            RuntimeDiagnostics.Record("CommunicationEvidenceCommit", "Started", pending.Batch.RunId,
                new { pending.Batch.EvidenceId, pending.Batch.WriteId, pending.Batch.BindingId });
            if (BeforeCommunicationCommit is not null) await BeforeCommunicationCommit(pending.Batch, pending.Cancellation);
            if (pending.Cancellation.IsCancellationRequested || !pending.Window.Contains(_clock.GetTimestamp()))
            { pending.Done.TrySetResult(result with { FailureReason = "EvidenceWindowClosedBeforeCommit" }); return; }
            result = await Task.Run(() => CommitCommunication(pending), _lifetime.Token);
            if (AfterCommunicationCommitBeforeReceipt is not null)
                await AfterCommunicationCommitBeforeReceipt(pending.Batch, result, _lifetime.Token);
            pending.Done.TrySetResult(result);
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("CommunicationEvidenceCommit", "ReceiptUnavailable", pending.Batch.RunId,
                new { pending.Batch.EvidenceId, pending.Batch.WriteId, actualCommit = result.ActualCommit.ToString() }, error);
            // A post-commit callback error does not turn an actual commit into a rollback.
            pending.Done.TrySetResult(result with { FailureReason = "EvidenceReceiptUnavailable:" + error.GetType().Name });
        }
        finally { _communicationPending.TryRemove(pending.Batch.EvidenceId, out _); }
    }

    private EvidenceCommitResult CommitCommunication(CommunicationPending pending)
    {
        using var ctx = new Station01DbContext(_options);
        IDbContextTransaction? transaction = null;
        var commitAttempted = false;
        try
        {
            transaction = ctx.Database.BeginTransaction();
            var batch = pending.Batch;
            var manifest = ctx.Manifests.AsNoTracking().Single();
            if (manifest.StoreId != batch.StoreId || manifest.SchemaVersion is not ("s01-store/2" or "s01-store/3"))
                throw new InvalidOperationException("EvidenceStoreIdentityMismatch");
            foreach (var reference in batch.PrecedingEvidenceReferences)
                if (!ctx.PlcCommunicationEvidence.AsNoTracking().Any(e => e.EvidenceId == reference.EvidenceId &&
                    e.StoreId == reference.StoreId && e.RunId == batch.RunId && e.OperationId == batch.OperationId &&
                    e.ActionId == batch.ActionId && e.ConnectionEpoch == batch.ConnectionEpoch))
                    throw new InvalidOperationException("PrecedingEvidenceNotCommittedForCurrentAction");
            var previous = ctx.PlcCommunicationEvidence.AsNoTracking().SingleOrDefault(e => e.EvidenceId == batch.EvidenceId);
            if (previous is not null)
            {
                if (previous.PayloadDigest != pending.Digest || previous.StoreId != batch.StoreId)
                    return new(ActualCommitState.Unknown, null, "EvidenceIdentityConflict");
                return new(ActualCommitState.Committed, previous.PersistedUtc, null);
            }
            if (pending.Cancellation.IsCancellationRequested || !pending.Window.Contains(_clock.GetTimestamp()))
            { transaction.Rollback(); return new(ActualCommitState.ConfirmedRolledBack, null, "EvidenceWindowClosedBeforeInsert"); }
            var persisted = _clock.GetUtcNow();
            ctx.PlcCommunicationEvidence.Add(new()
            {
                EvidenceId = batch.EvidenceId, StoreId = batch.StoreId, ObservationId = batch.ObservationId,
                RunId = batch.RunId, OperationId = batch.OperationId, ActionId = batch.ActionId,
                ConnectionEpoch = batch.ConnectionEpoch, ObservedStartUtc = batch.ObservedStartUtc,
                ObservedEndUtc = batch.ObservedEndUtc, PersistedUtc = persisted,
                PayloadSchema = batch.SchemaVersion, PayloadDigest = pending.Digest, RawPayloadJson = pending.Json
            });
            ctx.SaveChanges();
            commitAttempted = true;
            transaction.Commit();
            return new(ActualCommitState.Committed, persisted, null);
        }
        catch (Exception error)
        {
            var actual = ActualCommitState.Unknown;
            if (transaction is not null && !commitAttempted)
                try { transaction.Rollback(); actual = ActualCommitState.ConfirmedRolledBack; } catch { /* No rollback claim without confirmation. */ }
            RuntimeDiagnostics.Record("CommunicationEvidenceCommit", "Failed", pending.Batch.RunId,
                new { pending.Batch.EvidenceId, pending.Batch.WriteId, actualCommit = actual.ToString() }, error);
            return new(actual, null, error.GetType().Name + ":" + error.Message);
        }
        finally { transaction?.Dispose(); }
    }

    private sealed class CommunicationPending(CommunicationEvidenceBatch batch, string json, string digest,
        ActionWindow window, CancellationToken cancellation) : WriterJob
    {
        public CommunicationEvidenceBatch Batch { get; } = batch;
        public string Json { get; } = json;
        public string Digest { get; } = digest;
        public ActionWindow Window { get; } = window;
        public CancellationToken Cancellation { get; } = cancellation;
        public TaskCompletionSource<EvidenceCommitResult> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
