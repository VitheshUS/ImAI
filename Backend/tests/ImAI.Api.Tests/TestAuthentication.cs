using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ImAI.Api.Tests;

internal static class TestAuthentication
{
    public static void AddTestAuthentication(this IServiceCollection services) =>
        services.AddAuthentication("TestSession")
            .AddScheme<AuthenticationSchemeOptions, TestSessionHandler>("TestSession", _ => { });
}

internal sealed class TestSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], Scheme.Name)), Scheme.Name)));
}
