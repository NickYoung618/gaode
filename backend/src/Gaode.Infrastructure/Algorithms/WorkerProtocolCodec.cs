using System.Text;
using System.Text.Json;

namespace Gaode.Infrastructure.Algorithms;

/// <summary>
/// Validates the worker boundary. The worker receives controlled keys, never arbitrary
/// filesystem paths or image bytes, and each message is one UTF-8 line under 64 KiB.
/// </summary>
public static class WorkerProtocolCodec
{
    public const string ContractVersion = "station01-worker/2.0";
    public const int MaxLineBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static string Encode(WorkerMessage message)
    {
        Validate(message);
        var line = JsonSerializer.Serialize(message, Json) + "\n";
        if (Encoding.UTF8.GetByteCount(line) > MaxLineBytes)
            throw new InvalidDataException("Worker消息超过64KiB限制");
        return line;
    }

    public static WorkerMessage Decode(ReadOnlySpan<byte> utf8Line)
    {
        if (utf8Line.Length == 0 || utf8Line.Length > MaxLineBytes)
            throw new InvalidDataException("Worker消息长度无效");
        var line = Encoding.UTF8.GetString(utf8Line).TrimEnd('\r', '\n');
        if (line.Length == 0 || line.Contains('\n') || line.Contains('\r'))
            throw new InvalidDataException("Worker消息必须是单行NDJSON");
        WorkerMessage? message;
        try { message = JsonSerializer.Deserialize<WorkerMessage>(line, Json); }
        catch (JsonException error) { throw new InvalidDataException("Worker消息不是有效JSON", error); }
        if (message is null) throw new InvalidDataException("Worker消息为空");
        Validate(message);
        return message;
    }

    public static WorkerMessage Execute(Guid workerSessionId, WorkerExecutionInput input) => new(
        "Execute", ContractVersion, workerSessionId, input.CallId, input.Attempt,
        input.Role, input.LeaseId, Inputs: input.Inputs, ObservationContext: input.ObservationContext);

    private static void Validate(WorkerMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Type) ||
            message.ContractVersion != ContractVersion ||
            !KnownType(message.Type))
            throw new InvalidDataException("Worker消息信封无效");
        if (!message.IsControl && (message.CallId == Guid.Empty || message.Attempt < 1))
            throw new InvalidDataException("Worker执行消息缺少调用身份");
        if (message.Type is "Execute" or "Result" or "InputReleased" or "Cancel" &&
            (message.WorkerSessionId == Guid.Empty || string.IsNullOrWhiteSpace(message.Role)))
            throw new InvalidDataException("Worker执行消息缺少会话或角色");
        if (message.Type == "Execute")
        {
            if (string.IsNullOrWhiteSpace(message.LeaseId) ||
                message.Inputs is not { Count: 1 or 2 } ||
                message.Inputs.Any(input => string.IsNullOrWhiteSpace(input.LeaseId) ||
                    input.MediaId == Guid.Empty || input.CaptureId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(input.InputKey) ||
                    Path.IsPathFullyQualified(input.InputKey) ||
                    input.InputKey.Split('/', '\\').Contains("..") ||
                    input.ByteLength <= 0 || input.Sha256.Length != 64) ||
                message.Inputs.Select(input => input.MediaId).Distinct().Count() != message.Inputs.Count)
                throw new InvalidDataException("Worker输入必须是1或2个不同的受控媒体引用");
        }
        if (message.Type == "InputReleased" && message.InputIndex is null)
            throw new InvalidDataException("Worker释放消息缺少输入索引");
        if (message.ByteLength is < 0) throw new InvalidDataException("Worker输出长度无效");
        if (message.Type == "Result" && string.IsNullOrWhiteSpace(message.ResultJson))
            throw new InvalidDataException("Worker结果缺少数据");
    }

    private static bool KnownType(string type) => type is "Hello" or "Ready" or "Execute" or
        "Accepted" or "Result" or "Cancel" or "InputReleased" or "Health" or "Shutdown" or "WorkerExited";
}
