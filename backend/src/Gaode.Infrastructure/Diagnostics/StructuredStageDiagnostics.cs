using Microsoft.Extensions.Logging;
using Gaode.Application.Station01;

namespace Gaode.Infrastructure.Diagnostics;

public sealed class StructuredStageDiagnostics(ILogger<StructuredStageDiagnostics> logger) : IStartupDiagnosticSink
{
    public void Record(Guid runId, string stage, string code, string version, string disposition) =>
        logger.LogInformation("运行={RunId} 阶段={Stage} 错误码={Code} 版本={Version} 处理={Disposition}",
            runId, stage, code, version, disposition);

    public void Record(string requestId, Guid? commandId, Guid? runId, string stage,
        string classification, string disposition, long? connectionEpoch = null,
        DateTimeOffset? observedAtUtc = null, string? source = null,
        Exception? exception = null)
    {
        var category = stage is "StartupReadiness" || classification.StartsWith("Unconfirmed:") ||
            classification.StartsWith("ExplicitUnsafe:") ? "DeviceSafety" :
            stage is "CriticalSave" ? "Persistence" : "CommandFlow";
        var level = exception is not null ? LogLevel.Error :
            classification == "Accepted" ? LogLevel.Information : LogLevel.Warning;
        logger.Log(level, exception,
            "StartupDiagnostic category={Category} requestId={RequestId} commandId={CommandId} runId={RunId} stage={Stage} classification={Classification} disposition={Disposition} connectionEpoch={ConnectionEpoch} observedAtUtc={ObservedAtUtc:o} source={Source}",
            category, requestId, commandId, runId, stage, classification, disposition,
            connectionEpoch, observedAtUtc, source);
    }
}
