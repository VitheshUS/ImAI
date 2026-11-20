using ImAI.Application.Abstractions;
using ImAI.Application.Common;

namespace ImAI.Api.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ai/chat", async (AiChatRequest request, IAzureChatService chatService, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                throw new RequestValidationException("Query is required.");

            var response = await chatService.AskAsync(request.Query, cancellationToken);
            return Results.Text(response, "text/plain; charset=utf-8");
        })
        .WithName("AskAi").WithTags("AI")
        .Accepts<AiChatRequest>("application/json")
        .Produces<string>(StatusCodes.Status200OK, "text/plain")
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
        return endpoints;
    }
}

public sealed record AiChatRequest(string Query);
