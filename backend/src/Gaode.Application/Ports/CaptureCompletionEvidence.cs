using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gaode.Application.Ports;

public sealed record PersistedCapture(MediaRef Media, CaptureCompletionEvidence Completion);

public sealed record CaptureCompletionEvidence
{
    private CaptureCompletionEvidence() { }
    public Guid RunId { get; private init; }
    public Guid TrayId { get; private init; }
    public Guid WindowSessionId { get; private init; }
    public Guid WindowOperationId { get; private init; }
    public CaptureRequest Capture { get; private init; } = null!;
    public CorrelatedCaptureFact Fact { get; private init; } = null!;
    public MediaRef RawMedia { get; private init; } = null!;
    public Guid MediaWriteId { get; private init; }
    public Guid CaptureFactWriteId { get; private init; }
    public string RawSha256 { get; private init; } = "";
    public long PersistedRevision { get; private init; }
    public bool CaptureResourcesEnded { get; private init; }
    public bool DurableInputOwned { get; private init; }

    public CaptureWorkCommit ForWindow(AcquisitionSession session, Guid trayId)
    {
        var proof = this with { TrayId = trayId, WindowSessionId = session.SessionId,
            WindowOperationId = session.Request.Correlation.OperationId };
        if(!proof.IsFor(session)) throw new InvalidDataException("CaptureCompletionWindowMismatch");
        return new(proof);
    }
    public bool IsFor(AcquisitionSession session) => RunId == session.Request.Correlation.RunId && TrayId != Guid.Empty &&
        (session.Request.Correlation.TrayId is null || session.Request.Correlation.TrayId == TrayId) &&
        WindowSessionId == session.SessionId && WindowOperationId == session.Request.Correlation.OperationId &&
        Capture.AcquisitionSessionId == WindowSessionId && Capture.AcquisitionOperationId == WindowOperationId &&
        Capture.Envelope.SessionId == session.Request.Correlation.SessionId && Capture.Envelope.Attempt == session.Request.Correlation.Attempt &&
        Capture.Envelope.SnapshotId == session.Request.Correlation.SnapshotId &&
        Capture.Envelope.ClockId == session.Request.Window.ClockId &&
        Capture.Role == session.Request.Role && CaptureResourcesEnded && DurableInputOwned &&
        MediaWriteId != Guid.Empty && CaptureFactWriteId != Guid.Empty && MediaWriteId != CaptureFactWriteId && PersistedRevision > 0 &&
        Capture.Envelope.RunId == RunId && RawMedia.RunId == RunId && Fact.RunId == RunId &&
        Capture.CaptureId == Fact.CaptureId && RawMedia.CaptureId == Fact.CaptureId && RawMedia.AlgorithmInput is null &&
        Fact.OperationId == Capture.Envelope.OperationId && RawMedia.StorageState == "FileCompleted";

    public static async Task<CaptureCompletionEvidence> FromCommittedAsync(ITraceQuery queries, IMediaStore media,
        CaptureRequest capture, ReceivedCapture received, MediaRef raw, CommitReceipt mediaReceipt,
        CommitReceipt captureReceipt, CancellationToken token)
    {
        if (!received.ReliableCompletion || !media.IsReady(raw.MediaId) || raw.AlgorithmInput is not null ||
            capture.Envelope.RunId != raw.RunId || capture.CaptureId != raw.CaptureId ||
            raw.ByteLength != received.Bytes.LongLength || raw.Format != received.Format || raw.Source != received.Fact.MediaSource ||
            !AcquisitionContract.MatchesFact(received.Fact,capture,received.Fact.ConnectionEpoch))
            throw new InvalidDataException("CaptureCompletionSourceInvalid");
        var writes = await queries.GetWritesAsync(raw.RunId,token);
        PersistedWrite Verify(CommitReceipt receipt, WriteKind kind)
        {
            if(receipt.State != CommitState.Committed || receipt.RunId != raw.RunId || receipt.CommittedRevision is null ||
                receipt.RecordKind != BusinessCommitRecordKind.RunWrite) throw new InvalidDataException("CaptureCompletionReceiptInvalid");
            var write = writes.SingleOrDefault(x => x.WriteId == receipt.WriteId);
            if(write is null || write.Kind != kind || write.State != CommitState.Committed || write.Revision != receipt.CommittedRevision ||
                !StringComparer.OrdinalIgnoreCase.Equals(write.PayloadDigest,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(write.PayloadJson)))))
                throw new InvalidDataException("CaptureCompletionPersistedTypeOrDigestInvalid");
            return write;
        }
        var image = Verify(mediaReceipt,WriteKind.Media); var fact = Verify(captureReceipt,WriteKind.CaptureFact);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        if(JsonSerializer.Deserialize<MediaRef>(image.PayloadJson,json) != raw) throw new InvalidDataException("CaptureCompletionMediaMismatch");
        using var document = JsonDocument.Parse(fact.PayloadJson);
        var properties = document.RootElement.EnumerateObject().ToDictionary(x => x.Name,x => x.Value,StringComparer.OrdinalIgnoreCase);
        if(!properties.TryGetValue("captureFact",out var savedFact) ||
            JsonSerializer.Serialize(savedFact.Deserialize<CorrelatedCaptureFact>(json),json) != JsonSerializer.Serialize(received.Fact,json) ||
            !properties.TryGetValue("mediaId",out var id) || id.GetGuid() != raw.MediaId)
            throw new InvalidDataException("CaptureCompletionFactMismatch");
        return new() { RunId=raw.RunId,Capture=capture,Fact=received.Fact,RawMedia=raw,MediaWriteId=mediaReceipt.WriteId,
            CaptureFactWriteId=captureReceipt.WriteId,RawSha256=Convert.ToHexString(SHA256.HashData(received.Bytes)),
            PersistedRevision=captureReceipt.CommittedRevision!.Value,CaptureResourcesEnded=true,DurableInputOwned=true };
    }
}
