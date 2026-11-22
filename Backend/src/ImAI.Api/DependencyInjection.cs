using ImAI.Api.Middleware;
using ImAI.Api.Sessions;
using ImAI.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
namespace ImAI.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<SessionCookie>();
        services.AddScoped<ISessionTokenWriter>(provider => provider.GetRequiredService<SessionCookie>());
        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 51 * 1024 * 1024);
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = 51 * 1024 * 1024);
        services.AddCors(options => options.AddPolicy("Frontend", policy => policy
            .WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200", "https://localhost:4200"])
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        return services;
    }
}
