using ImAI.Api.Extensions;
using ImAI.Application.Chat;
using MediatR;

namespace ImAI.Api.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ai/chat", async (AiChatRequest request, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
        {
            var session = context.GetCurrentSession()
                ?? throw new InvalidOperationException("Authenticated session context is missing.");
            return Results.Ok(await sender.Send(new SendChatQuery(session.SessionId, request.Query, request.ConversationId), cancellationToken));
        })
            .RequireAuthorization().Produces(401).WithName("AskAi").WithTags("AI")
            .Accepts<AiChatRequest>("application/json")
            .Produces<AiChatResponse>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409).ProducesProblem(500);
        return endpoints;
    }
}

public sealed record AiChatRequest(string Query, Guid? ConversationId = null);
