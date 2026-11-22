using ImAI.Api.Sessions;
using ImAI.Application.Abstractions;
using ImAI.Application.Sessions;

namespace ImAI.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/sessions").WithTags("Sessions");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.Pragma = "no-cache";
            return await next(context);
        });

        group.MapPost("", async (IAnonymousSessionService sessions, CancellationToken cancellationToken) =>
            Results.Ok(await sessions.CreateAsync(cancellationToken))).AllowAnonymous().WithName("CreateAnonymousSession").Produces<SessionResponse>();

        group.MapPost("/validate", (HttpContext context) =>
        {
            var session = context.GetCurrentSession()
                ?? throw new InvalidOperationException("Authenticated session context is missing.");
            return Results.Ok(session);
        }).RequireAuthorization().WithName("ValidateAnonymousSession").Produces<SessionResponse>().Produces(401);

        group.MapPost("/refresh", async (HttpContext context, IAnonymousSessionService sessions, SessionCookie cookie, CancellationToken cancellationToken) =>
        {
            var session = await sessions.RefreshAsync(cookie.Read(context), cancellationToken);
            return session is null ? Results.Unauthorized() : Results.Ok(session);
        }).RequireAuthorization().WithName("RefreshAnonymousSession").Produces<SessionResponse>().Produces(401);
        return endpoints;
    }
}
