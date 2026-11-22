using ImAI.Application.Chat;
using MediatR;

namespace ImAI.Api.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ai/chat", async (SendChatQuery request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(request, cancellationToken)))
            .RequireAuthorization().Produces(401).WithName("AskAi").WithTags("AI")
            .Accepts<SendChatQuery>("application/json")
            .Produces<AiChatResponse>()
            .ProducesProblem(400).ProducesProblem(500);
        return endpoints;
    }
}
