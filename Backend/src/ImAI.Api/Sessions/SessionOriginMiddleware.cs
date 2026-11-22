namespace ImAI.Api.Sessions;

// Protect cookie-authenticated POSTs (including multipart uploads) from cross-origin form submissions.
public sealed class SessionOriginMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method))
        {
            var origin = context.Request.Headers.Origin.ToString();
            var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200", "https://localhost:4200"];
            var sameOrigin = $"{context.Request.Scheme}://{context.Request.Host}";
            if ((!string.IsNullOrEmpty(origin) && origin != sameOrigin && !allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
                || (string.IsNullOrEmpty(origin) && context.Request.Headers["Sec-Fetch-Site"] == "cross-site"))
            {
                await Results.Problem(statusCode: 403, title: "Request origin is not allowed.").ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
