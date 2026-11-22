using ImAI.Application.Sessions;

namespace ImAI.Api.Extensions;

public static class CurrentSessionExtensions
{
    public static SessionResponse? GetCurrentSession(this HttpContext context) =>
        context.Features.Get<SessionResponse>();
}
