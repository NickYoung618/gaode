using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Gaode.Infrastructure.Diagnostics;

// Bridges BCL diagnostic events without changing any public station/PLC contract.
public sealed class RuntimeDiagnosticLogging : IObserver<DiagnosticListener>,
    IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly ILogger logger;
    private readonly List<IDisposable> subscriptions = [];
    private readonly IDisposable all;
    private bool disposed;

    public RuntimeDiagnosticLogging(ILogger logger)
    {
        this.logger = logger;
        all = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public void OnNext(DiagnosticListener value)
    {
        if (value.Name != "Gaode.Runtime") return;
        lock (subscriptions)
        {
            if (!disposed) subscriptions.Add(value.Subscribe(this, name => name == "RuntimeFlow"));
        }
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Value is not IReadOnlyDictionary<string, object?> fields) return;
        var level = fields["level"]?.ToString() switch
        {
            "Error" => LogLevel.Error, "Warning" => LogLevel.Warning, _ => LogLevel.Information
        };
        if (!logger.IsEnabled(level)) return;
        var exception = fields["exception"] as Exception;
        // No request bodies, authorization headers, image bytes or algorithm
        // payloads: producers select only diagnostic identities/decisions.
        var document = fields.Where(x => x.Key != "exception").ToDictionary(x => x.Key, x => x.Value);
        document["exceptionType"] = exception?.GetType().FullName;
        logger.Log(level, exception, "RuntimeFlow {RuntimeJson}", JsonSerializer.Serialize(document));
    }

    public void OnError(Exception error) => logger.LogError(error, "Runtime diagnostic subscription failed");
    public void OnCompleted() { }
    public void Dispose()
    {
        lock (subscriptions)
        {
            disposed = true;
            foreach (var subscription in subscriptions) subscription.Dispose();
            subscriptions.Clear();
        }
        all.Dispose();
    }
}
