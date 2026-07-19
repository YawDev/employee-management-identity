namespace employee.management.identity.Middleware
{
    /// <summary>
    /// Assigns each request a correlation id (from the incoming X-Correlation-ID header or a
    /// new GUID), echoes it back on the response, and pushes it into a logging scope so every
    /// log line for the request carries it. Uses the same header as the EMT microservice so a
    /// login -> token flow can be traced end to end across services.
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
                await _next(context);
            }
        }
    }
}
