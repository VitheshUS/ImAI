using ImAI.Application.Abstractions;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using ImAI.Infrastructure.Chat;
using ImAI.Infrastructure.Persistence;
using ImAI.Infrastructure.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ImAI.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
namespace ImAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ImAiDbContext>(options => options.UseSqlite(
            configuration.GetConnectionString("ImAI") ?? SqliteDatabase.DefaultConnectionString));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IAnonymousSessionService, AnonymousSessionService>();
        services.AddSingleton<IChatClient>(_ =>
        {
            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var deployment = configuration["AzureOpenAI:ChatDeployment"];
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Configure AzureOpenAI:Endpoint with a valid HTTPS endpoint.");
            if (string.IsNullOrWhiteSpace(deployment))
                throw new InvalidOperationException("Configure AzureOpenAI:ChatDeployment with the Azure deployment name.");
            return new AzureOpenAIClient(uri, new AzureCliCredential()).GetChatClient(deployment).AsIChatClient();
        });
        services.AddScoped<IAzureChatService, AzureChatService>();
        services.AddScoped<IDocumentStorage, StubDocumentStorage>();
        return services;
    }
}
