using ImAI.Application.Abstractions;
using ImAI.Infrastructure.Chat;
using ImAI.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
namespace ImAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAzureChatService>(_ =>
        {
            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var deployment = configuration["AzureOpenAI:ChatDeployment"];
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Configure AzureOpenAI:Endpoint with a valid HTTPS endpoint.");
            if (string.IsNullOrWhiteSpace(deployment))
                throw new InvalidOperationException("Configure AzureOpenAI:ChatDeployment with the Azure deployment name.");
            return new AzureChatService(endpoint!, deployment);
        });
        services.AddScoped<IChatResponder, DummyChatResponder>();
        services.AddScoped<IDocumentStorage, StubDocumentStorage>();
        return services;
    }
}
