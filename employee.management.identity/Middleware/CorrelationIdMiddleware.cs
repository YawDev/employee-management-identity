using System.Diagnostics;

namespace employee.management.identity.Middleware
{
    /// <summary>
    /// Assigns each request a correlation id (from the incoming X-Correlation-ID header or a new
    /// GUID), echoes it back on the response, pushes it into a logging scope so every log line for
    /// the request carries it, and logs one request-completion line (method, status, elapsed).
    /// The frontend/BFF can supply one shared id across services for end-to-end tracing.
    /// </summary>
    public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        public const string HeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next = next;
        private readonly ILogger<CorrelationIdMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
                                && !string.IsNullOrWhiteSpace(value)
                ? value.ToString()
                : Guid.NewGuid().ToString();

            context.Response.Headers[HeaderName] = correlationId;

            // Message-template scope (not a Dictionary): renders as "CorrelationId:<id>" in the
            // plain console AND exposes a structured "CorrelationId" key for JSON/structured sinks.
            using (_logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
            {
                // Skip the noisy Swagger/OpenAPI polling from the request-completion log.
                var logSummary = !context.Request.Path.StartsWithSegments("/swagger");
                var stopwatch = logSummary ? Stopwatch.StartNew() : null;

                await _next(context);

                if (stopwatch is not null)
                {
                    stopwatch.Stop();
                    _logger.LogInformation("{Method} responded {StatusCode} in {ElapsedMs}ms",
                        context.Request.Method, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
                }
            }
        }
    }
}
