using System.Security.Claims;
using System.Text.Encodings.Web;
using ImAI.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ImAI.Api.Sessions;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IAnonymousSessionService sessions, SessionCookie cookie)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AnonymousSession";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = cookie.Read(Context);
        if (token is null) return AuthenticateResult.NoResult();
        var session = await sessions.ValidateAsync(token, Context.RequestAborted);
        if (session is null)
        {
            cookie.Clear(Context);
            return AuthenticateResult.Fail("Session is invalid or expired.");
        }
        Context.Features.Set(session);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, session.SessionId.ToString())], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
