using ImAI.Application.Abstractions;
using ImAI.Application.Configuration;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ImAI.Infrastructure.Chat;
using ImAI.Infrastructure.Persistence;
using ImAI.Infrastructure.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ImAI.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Globalization;
namespace ImAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging();
        services.AddDbContext<ImAiDbContext>(options => options.UseSqlite(
            configuration.GetConnectionString("ImAI") ?? SqliteDatabase.DefaultConnectionString));
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IAnonymousSessionService, AnonymousSessionService>();
        services.AddScoped<IConversationStore, ConversationStore>();
        services.AddScoped<IConversationContextReader, ConversationContextReader>();
        services.AddOptions<ConversationSummaryOptions>()
            .Configure(options =>
            {
                var value = configuration[$"{ConversationSummaryOptions.SectionName}:KLimit"];
                if (value is null) return;
                if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit))
                    throw new InvalidOperationException("ConversationSummary:KLimit must be a positive integer.");
                options.KLimit = limit;
            })
            .Validate(options => options.KLimit > 0, "ConversationSummary:KLimit must be greater than zero.")
            .ValidateOnStart();
        services.AddOptions<ConversationContextOptions>()
            .Configure(options => options.SystemInstructions =
                configuration[$"{ConversationContextOptions.SectionName}:SystemInstructions"] ?? string.Empty)
            .Validate(options => !string.IsNullOrWhiteSpace(options.SystemInstructions),
                "ConversationContext:SystemInstructions is required.")
            .ValidateOnStart();
        services.AddSingleton<TokenCredential>(provider =>
        {
            var development = provider.GetService<IHostEnvironment>()?.IsDevelopment() == true;
            var mode = configuration["AzureOpenAI:Credential"] ?? (development ? "AzureCli" : "Default");
            var tenantId = configuration["AzureOpenAI:TenantId"];
            if (string.Equals(mode, "AzureCli", StringComparison.OrdinalIgnoreCase))
            {
                var cliDirectory = configuration["AzureOpenAI:AzureCliDirectory"];
                if (!string.IsNullOrWhiteSpace(cliDirectory))
                {
                    cliDirectory = Path.GetFullPath(cliDirectory);
                    if (!File.Exists(Path.Combine(cliDirectory, OperatingSystem.IsWindows() ? "az.cmd" : "az")))
                        throw new InvalidOperationException("AzureOpenAI:AzureCliDirectory must contain the Azure CLI executable.");
                    var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                    if (!currentPath.Split(Path.PathSeparator).Contains(cliDirectory, StringComparer.OrdinalIgnoreCase))
                        Environment.SetEnvironmentVariable("PATH", cliDirectory + Path.PathSeparator + currentPath);
                }
                return new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId });
            }
            if (!string.Equals(mode, "Default", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("AzureOpenAI:Credential must be AzureCli or Default.");
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                TenantId = tenantId,
                ExcludeManagedIdentityCredential = development
            });
        });
        services.AddSingleton<IChatClient>(provider =>
        {
            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var deployment = configuration["AzureOpenAI:ChatDeployment"];
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Configure AzureOpenAI:Endpoint with a valid HTTPS endpoint.");
            if (string.IsNullOrWhiteSpace(deployment))
                throw new InvalidOperationException("Configure AzureOpenAI:ChatDeployment with the Azure deployment name.");
            var options = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21)
            {
                Audience = AzureOpenAIAudience.AzurePublicCloud
            };
            var credential = provider.GetRequiredService<TokenCredential>();
            provider.GetService<ILogger<AzureChatService>>()?.LogInformation(
                "Azure OpenAI endpoint={Endpoint}, deployment={Deployment}, credential={Credential}, tenant={TenantId}, apiVersion=2024-10-21",
                uri.GetLeftPart(UriPartial.Authority), deployment, credential.GetType().Name, configuration["AzureOpenAI:TenantId"]);
            return new AzureOpenAIClient(uri, credential, options)
                .GetChatClient(deployment).AsIChatClient();
        });
        services.AddScoped<IAzureChatService, AzureChatService>();
        services.AddScoped<IDocumentStorage, StubDocumentStorage>();
        return services;
    }
}
