namespace Gaode.Application.Station01;

public interface IStartupDiagnosticSink
{
    void Record(string requestId, Guid? commandId, Guid? runId, string stage,
        string classification, string disposition, long? connectionEpoch = null,
        DateTimeOffset? observedAtUtc = null, string? source = null,
        Exception? exception = null);
}
