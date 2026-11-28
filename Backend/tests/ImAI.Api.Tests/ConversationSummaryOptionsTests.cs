using ImAI.Application.Configuration;
using ImAI.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ImAI.Api.Tests;

public sealed class ConversationSummaryOptionsTests
{
    [Theory]
    [InlineData(null, 10L)]
    [InlineData("7", 7L)]
    public void ReadsConfiguredKLimitOrUsesTheDefault(string? value, long expected)
    {
        using var provider = CreateProvider(value);
        Assert.Equal(expected, provider.GetRequiredService<IOptions<ConversationSummaryOptions>>().Value.KLimit);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void RejectsNonPositiveKLimits(string value)
    {
        using var provider = CreateProvider(value);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ConversationSummaryOptions>>().Value);
    }

    [Fact]
    public void RejectsNonNumericKLimits()
    {
        using var provider = CreateProvider("invalid");
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<ConversationSummaryOptions>>().Value);
    }

    private static ServiceProvider CreateProvider(string? value) => new ServiceCollection()
        .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConversationSummary:KLimit"] = value
        }).Build()).BuildServiceProvider();
}
