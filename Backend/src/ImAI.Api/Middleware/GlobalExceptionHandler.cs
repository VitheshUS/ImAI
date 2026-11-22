using ImAI.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
namespace ImAI.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var status = exception switch
        {
            RequestValidationException => StatusCodes.Status400BadRequest,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => StatusCodes.Status500InternalServerError
        };
        if (status >= 500) logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", context.TraceIdentifier);
        await Results.Problem(statusCode: status,
            title: status >= 500 ? "An unexpected error occurred." : "Request rejected.",
            detail: exception is RequestValidationException ? exception.Message : null)
            .ExecuteAsync(context);
        return true;
    }
}
