using ImAI.Application.Chat;
using MediatR;
namespace ImAI.Api.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/chat", async (SendChatQuery request, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(request, cancellationToken)))
            .WithName("SendChatQuery").WithTags("Chat").Produces<ChatReply>()
            .ProducesProblem(400).ProducesProblem(500);
        return endpoints;
    }
}
