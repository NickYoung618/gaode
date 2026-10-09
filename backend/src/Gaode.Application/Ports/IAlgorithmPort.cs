namespace Gaode.Application.Ports;

// Successful completion alone is reliable execution-end evidence. Fault/cancellation is not.
public sealed record AlgorithmDispatch(Task Exited);

// Adapter assertion: rejected before acquiring/forwarding any input, starting any work,
// or publishing Accepted/Running/Result. An ordinary exception supplies no such evidence.
public sealed class AlgorithmNotDispatchedException(Guid callId, string evidence)
    : Exception("算法未派发且未取得输入：" + evidence)
{
    public Guid CallId { get; } = callId != Guid.Empty ? callId
        : throw new ArgumentException("拒绝依据必须关联调用", nameof(callId));
    public string Evidence { get; } = !string.IsNullOrWhiteSpace(evidence) ? evidence
        : throw new ArgumentException("未派发拒绝必须提供依据", nameof(evidence));
}

public interface IAlgorithmPort
{
    AlgorithmInputRepresentation InputRepresentation(AlgorithmRole role) => AlgorithmInputRepresentation.NativeMedia;
    Gaode.Domain.Station01.ComponentExecutionOrigin Origin => Gaode.Domain.Station01.ComponentExecutionOrigin.Unknown;
    // Runs behind a bounded per-role isolation boundary, including the synchronous prefix.
    // Cancellation is a request, not execution-end/input-release evidence. Ordinary throws
    // (before or after Accepted) leave dispatch unknown. Matching InputReleased releases
    // only media; WorkerExited or successful Exited also establishes execution end.
    ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request, Action<AlgorithmEvent> onEvent,
        CancellationToken cancellationToken);
    int CallCount(AlgorithmRole role);
}
public enum AlgorithmInputRepresentation { NativeMedia, Png, Ply }
