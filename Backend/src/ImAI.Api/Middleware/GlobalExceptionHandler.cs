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
            AiServiceUnavailableException => StatusCodes.Status503ServiceUnavailable,
            RequestValidationException => StatusCodes.Status400BadRequest,
            ConversationNotFoundException => StatusCodes.Status404NotFound,
            ConversationConflictException => StatusCodes.Status409Conflict,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => StatusCodes.Status500InternalServerError
        };
        if (status >= 500) logger.LogError(exception, "Unhandled request failure. TraceId: {TraceId}", context.TraceIdentifier);
        await Results.Problem(statusCode: status,
            title: exception is AiServiceUnavailableException ? "AI service is unavailable."
                : status >= 500 ? "An unexpected error occurred." : "Request rejected.",
            detail: exception is AiServiceUnavailableException or RequestValidationException or ConversationNotFoundException or ConversationConflictException ? exception.Message : null)
            .ExecuteAsync(context);
        return true;
    }
}
