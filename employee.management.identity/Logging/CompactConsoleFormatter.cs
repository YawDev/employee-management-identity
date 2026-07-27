using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace employee.management.identity.Logging
{
    /// <summary>
    /// Single-line console formatter: "{level}: [{CorrelationId}] {RequestPath} {message}".
    /// Reads only CorrelationId + RequestPath out of the ambient scopes and drops the framework
    /// scope noise (SpanId / TraceId / ParentId / ConnectionId / RequestId). Renders each line in
    /// ANSI blue (TTY only). Development-only — hosted environments use the JSON console.
    /// </summary>
    public sealed class CompactConsoleFormatter() : ConsoleFormatter(FormatterName)
    {
        public const string FormatterName = "compact";
        private static readonly string Esc = ((char)27).ToString();
        private static readonly string Blue = Esc + "[94m";  // ANSI bright blue
        private static readonly string Reset = Esc + "[0m";
        private static readonly bool Colorize = !Console.IsOutputRedirected;

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

            if (Colorize) textWriter.Write(Blue);
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
            if (Colorize) textWriter.Write(Reset);
            textWriter.Write(Environment.NewLine);
        }
    }
}
