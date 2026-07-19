using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace employee.management.identity.Logging
{
    /// <summary>
    /// Minimal single-line console formatter: "{level}: [{CorrelationId}] {RequestPath} {message}".
    /// Reads only CorrelationId + RequestPath out of the ambient scopes and drops the framework
    /// scope noise (SpanId / TraceId / ParentId / ConnectionId / RequestId). Development-only —
    /// hosted environments use the JSON console so aggregators can query the fields.
    /// </summary>
    public sealed class CompactConsoleFormatter() : ConsoleFormatter(FormatterName)
    {
        public const string FormatterName = "compact";

        public override void Write<TState>(
            in LogEntry<TState> logEntry,
            IExternalScopeProvider? scopeProvider,
            TextWriter textWriter)
        {
            var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
            if (string.IsNullOrEmpty(message) && logEntry.Exception is null)
                return;

            string? correlationId = null;
            string? requestPath = null;

            scopeProvider?.ForEachScope((scope, _) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    foreach (var pair in pairs)
                    {
                        if (pair.Key == "CorrelationId") correlationId = pair.Value?.ToString();
                        else if (pair.Key == "RequestPath") requestPath = pair.Value?.ToString();
                    }
                }
            }, textWriter);

            var level = logEntry.LogLevel switch
            {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => "info",
            };

            textWriter.Write(level);
            textWriter.Write(": [");
            textWriter.Write(correlationId ?? "-");
            textWriter.Write("] ");
            if (!string.IsNullOrEmpty(requestPath))
            {
                textWriter.Write(requestPath);
                textWriter.Write(' ');
            }
            textWriter.Write(message);
            if (logEntry.Exception is not null)
            {
                textWriter.Write(' ');
                textWriter.Write(logEntry.Exception.ToString());
            }
            textWriter.Write(Environment.NewLine);
        }
    }
}
