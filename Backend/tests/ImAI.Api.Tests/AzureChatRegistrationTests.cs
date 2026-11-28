using ImAI.Application.Abstractions;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using ImAI.Infrastructure.DependencyInjection;
using ImAI.Infrastructure.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace ImAI.Api.Tests;

public sealed class AzureChatRegistrationTests
{
    [Fact]
    public void DevelopmentUsesCliInsteadOfAnUnintendedVisualStudioIdentity()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new DevelopmentEnvironment());
        services.AddInfrastructure(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        Assert.IsType<AzureCliCredential>(provider.GetRequiredService<TokenCredential>());
        Assert.Single(services, service => service.ServiceType == typeof(TokenCredential));
        Assert.Single(services, service => service.ServiceType == typeof(IChatClient));
    }

    [Theory]
    [InlineData("AzureCli", typeof(AzureCliCredential))]
    [InlineData("Default", typeof(DefaultAzureCredential))]
    public void CredentialChoiceIsExplicit(string mode, Type expected)
    {
        using var provider = new ServiceCollection().AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureOpenAI:Credential"] = mode }).Build()).BuildServiceProvider();
        Assert.Equal(expected, provider.GetRequiredService<TokenCredential>().GetType());
    }

    [Fact]
    public void UnknownCredentialChoiceDoesNotSilentlyFallBack()
    {
        using var provider = new ServiceCollection().AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AzureOpenAI:Credential"] = "Wrong" }).Build()).BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<TokenCredential>());
    }

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ImAI.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

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
        using var otherScope = provider.CreateScope();
        Assert.Same(scope.ServiceProvider.GetRequiredService<IChatClient>(), otherScope.ServiceProvider.GetRequiredService<IChatClient>());
        Assert.Same(service, scope.ServiceProvider.GetRequiredService<IAzureChatService>());
    }

    [Fact]
    public void MissingAzureSettingsOnlyFailWhenAzureServiceIsRequested()
    {
        using var provider = new ServiceCollection()
            .AddInfrastructure(new ConfigurationBuilder().Build()).BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDocumentStorage>());
        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IAzureChatService>());
        Assert.Contains("AzureOpenAI:Endpoint", exception.Message);
    }
}
