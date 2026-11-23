using ImAI.Api.Extensions;
using ImAI.Application.Chat;
using MediatR;

namespace ImAI.Api.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/conversations").WithTags("Conversations").RequireAuthorization();
        group.MapGet("", async (HttpContext context, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new ListConversations(SessionId(context)), cancellationToken)))
            .WithName("ListConversations").Produces<IReadOnlyList<ConversationOverview>>().Produces(401);
        group.MapGet("/{id:guid}", async (Guid id, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
            Results.Ok(await sender.Send(new GetConversation(SessionId(context), id), cancellationToken)))
            .WithName("GetConversation").Produces<ConversationDetails>().Produces(401).ProducesProblem(404);
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext context, ISender sender, CancellationToken cancellationToken) =>
        {
            await sender.Send(new DeleteConversation(SessionId(context), id), cancellationToken);
            return Results.NoContent();
        }).WithName("DeleteConversation").Produces(204).Produces(401).ProducesProblem(404);
        return endpoints;
    }

    private static Guid SessionId(HttpContext context) => context.GetCurrentSession()?.SessionId
        ?? throw new InvalidOperationException("Authenticated session context is missing.");
}
