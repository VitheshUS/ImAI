using ImAI.Api.Configuration;

namespace ImAI.Api.Middleware;

// Protect cookie-authenticated POSTs (including multipart uploads) from cross-origin form submissions.
public sealed class SessionOriginMiddleware(RequestDelegate next, BrowserOrigins origins)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method))
        {
            var origin = context.Request.Headers.Origin.ToString();
            if ((!string.IsNullOrEmpty(origin) && !origins.Allows(origin, context.Request))
                || (string.IsNullOrEmpty(origin) && context.Request.Headers["Sec-Fetch-Site"] == "cross-site"))
            {
                await Results.Problem(statusCode: 403, title: "Request origin is not allowed.").ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
