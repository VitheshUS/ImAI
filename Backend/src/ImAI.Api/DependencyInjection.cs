using ImAI.Api.Middleware;
using ImAI.Api.Authentication;
using ImAI.Api.Configuration;
using ImAI.Api.Cookies;
using ImAI.Application.Abstractions;
using ImAI.Application.Documents;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
namespace ImAI.Api;

public static class DependencyInjection
{
    private const long MaxRequestBodySize = ReceiveDocumentHandler.MaxFileSize + 1024 * 1024;
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<SessionCookie>();
        services.AddScoped<ISessionTokenWriter>(provider => provider.GetRequiredService<SessionCookie>());
        services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddOpenApi();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxRequestBodySize);
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options => options.Limits.MaxRequestBodySize = MaxRequestBodySize);
        var origins = new BrowserOrigins(configuration);
        services.AddSingleton(origins);
        services.AddCors(options => options.AddPolicy("Frontend", policy => policy
            .WithOrigins(origins.Allowed)
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        return services;
    }
}
