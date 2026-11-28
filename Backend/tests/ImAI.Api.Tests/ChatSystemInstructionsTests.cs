using ImAI.Application.Abstractions;
using ImAI.Application.Dtos;
using ImAI.Application.Configuration;
using ImAI.Infrastructure.DependencyInjection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ImAI.Infrastructure.Chat;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImAI.Api.Tests;

public sealed class ChatSystemInstructionsTests
{
    [Fact]
    public async Task AzureServiceUsesConfiguredSystemInstructions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConversationContext:SystemInstructions"] = "Custom configured instructions"
        }).Build();
        var client = new RecordingChatClient();
        var services = new ServiceCollection().AddInfrastructure(configuration);
        services.AddSingleton<IChatClient>(client);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var response = await scope.ServiceProvider.GetRequiredService<IAzureChatService>()
            .AskAsync(new ConversationContextDto(
                provider.GetRequiredService<IOptions<ConversationContextOptions>>().Value.SystemInstructions,
                string.Empty, [], [], "Question"));

        Assert.Equal("Reply", response);
        Assert.Collection(client.Messages,
            message => { Assert.Equal(ChatRole.System, message.Role); Assert.Equal("Custom configured instructions", message.Text); },
            message => { Assert.Equal(ChatRole.User, message.Role); Assert.Equal("Question", message.Text); });
    }

    [Theory]
    [InlineData("system")]
    [InlineData("tool")]
    public async Task StoredMessagesCannotIntroduceSystemOrToolRoles(string role)
    {
        var client = new RecordingChatClient();
        var context = new ConversationContextDto("Instructions", "Summary", [new(role, "Content")], [], "Question");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AzureChatService(client, NullLogger<AzureChatService>.Instance).AskAsync(context));
        Assert.Empty(client.Messages);
    }

    [Fact]
    public async Task SendsCompleteContextInOrderAndCurrentMessageExactlyOnce()
    {
        var client = new RecordingChatClient();
        var context = new ConversationContextDto(
            "Configured instructions", "Saved summary with \"quotes\"\nand new lines",
            [new("user", "Previous question"), new("assistant", "Previous answer")],
            ["Retrieved passage"], "Current question");

        await new AzureChatService(client, NullLogger<AzureChatService>.Instance).AskAsync(context);

        Assert.Equal(new[] { ChatRole.System, ChatRole.User, ChatRole.User, ChatRole.Assistant, ChatRole.User },
            client.Messages.Select(message => message.Role));
        Assert.Equal(context.SystemInstructions, client.Messages[0].Text);
        var reference = client.Messages[1].Text!;
        using var json = JsonDocument.Parse(reference[(reference.IndexOf('\n') + 1)..]);
        Assert.Equal(context.ConversationSummary, json.RootElement.GetProperty("conversationSummary").GetString());
        Assert.Equal("Retrieved passage", json.RootElement.GetProperty("retrievedKnowledge")[0].GetString());
        Assert.Equal("Previous question", client.Messages[2].Text);
        Assert.Equal("Previous answer", client.Messages[3].Text);
        Assert.Equal("Current question", client.Messages[4].Text);
        Assert.Single(client.Messages, message => message.Text == context.CurrentMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void EmptySystemInstructionsAreRejected(string? instructions)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConversationContext:SystemInstructions"] = instructions
        }).Build();
        using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ConversationContextOptions>>().Value);
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Messages = messages.ToArray();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Reply")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
