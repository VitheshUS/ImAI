using System.Diagnostics;

namespace ImAI.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier,
            ["RequestId"] = context.TraceIdentifier,
            ["HttpMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value
        });
        var started = Stopwatch.GetTimestamp();
        logger.LogInformation(new EventId(1000, "HttpRequestStarted"), "HTTP request started");
        await next(context);
        logger.LogInformation(new EventId(1001, "HttpRequestCompleted"),
            "HTTP request completed with status {StatusCode} in {ElapsedMilliseconds} ms",
            context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
}
