using ImAI.Application.Abstractions;
using ImAI.Infrastructure;
using ImAI.Infrastructure.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ImAI.Api.Tests;

public sealed class AzureChatRegistrationTests
{
    [Fact]
    public void ResolvesAzureChatServiceFromApplicationInterface()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureOpenAI:Endpoint"] = "https://imai-test.openai.azure.com/",
            ["AzureOpenAI:ChatDeployment"] = "test-chat"
        }).Build();
        using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAzureChatService>();
        Assert.IsType<AzureChatService>(service);
        Assert.Same(service, scope.ServiceProvider.GetRequiredService<IAzureChatService>());
    }

    [Fact]
    public void MissingAzureSettingsOnlyFailWhenAzureServiceIsRequested()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(new ConfigurationBuilder().Build()).BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IChatResponder>());
        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IAzureChatService>());
        Assert.Contains("AzureOpenAI:Endpoint", exception.Message);
    }
}
