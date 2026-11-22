using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using ImAI.Application.Chat;

namespace ImAI.Api.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ai/chat", async (AiChatRequest request, IAzureChatService chatService, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                throw new RequestValidationException("Query is required.");

            if (request.ConversationId == Guid.Empty)
                throw new RequestValidationException("ConversationId must be a non-empty UUID when supplied.");

            var response = await chatService.AskAsync(request.Query, cancellationToken);
            return Results.Ok(new AiChatResponse(
                request.ConversationId ?? Guid.NewGuid(), Guid.NewGuid(), response, []));
        })
        .WithName("AskAi").WithTags("AI")
        .Accepts<AiChatRequest>("application/json")
        .Produces<AiChatResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
        return endpoints;
    }
}

public sealed record AiChatRequest(string Query, Guid? ConversationId = null);
