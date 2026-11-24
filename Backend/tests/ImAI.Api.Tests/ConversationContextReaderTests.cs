using ImAI.Application.Abstractions;
using ImAI.Domain.Entities;
using ImAI.Infrastructure;
using ImAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ImAI.Api.Tests;

public sealed class ConversationContextReaderTests : IAsyncLifetime
{
    private const string Instructions = "Configured instructions for this test.";
    private readonly ServiceProvider provider;
    private readonly AsyncServiceScope scope;
    private readonly ImAiDbContext database;
    private readonly IConversationContextReader reader;

    public ConversationContextReaderTests()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ImAI"] = "Data Source=:memory:",
            ["ConversationContext:SystemInstructions"] = Instructions
        }).Build();
        provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        scope = provider.CreateAsyncScope();
        database = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        reader = scope.ServiceProvider.GetRequiredService<IConversationContextReader>();
    }

    public async Task InitializeAsync()
    {
        await database.Database.OpenConnectionAsync();
        await database.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await scope.DisposeAsync();
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task NoSummaryReturnsNullEvenWhenMessagesExist()
    {
        var conversation = await SeedAsync(null);
        Assert.Null(await reader.ReadAsync(conversation.SessionId, conversation.Id, "next question"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task ProjectsOnlyMessagesAfterSummaryBoundaryInSequenceOrder(long boundary)
    {
        var conversation = await SeedAsync(boundary);
        // Another conversation in the same session must not leak into the context.
        database.Conversations.Add(new Conversation
        {
            Id = Guid.NewGuid(),
            SessionId = conversation.SessionId,
            Summary = new ConversationSummary { Content = "Other summary" },
            ChatMessages = [new ChatMessage { Id = Guid.NewGuid(), Sequence = 99, Role = "user", Content = "Other conversation" }]
        });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var context = await reader.ReadAsync(conversation.SessionId, conversation.Id, "current question");

        Assert.NotNull(context);
        Assert.Equal(Instructions, context.SystemInstructions);
        Assert.Equal("Saved summary", context.ConversationSummary);
        Assert.Equal("current question", context.CurrentMessage);
        Assert.Empty(context.RetrievedKnowledge);
        var expected = Enumerable.Range(1, 4).Where(sequence => sequence > boundary).ToArray();
        Assert.Equal(expected.Select(sequence => $"Message {sequence}"), context.RecentMessages.Select(message => message.Content));
        Assert.Equal(expected.Select(sequence => sequence % 2 == 1 ? "user" : "assistant"), context.RecentMessages.Select(message => message.Role));
        Assert.Empty(database.ChangeTracker.Entries());
    }

    [Fact]
    public async Task UnknownAndOtherSessionsConversationsReturnNull()
    {
        var conversation = await SeedAsync(2);
        Assert.Null(await reader.ReadAsync(Guid.NewGuid(), conversation.Id, "question"));
        Assert.Null(await reader.ReadAsync(conversation.SessionId, Guid.NewGuid(), "question"));
    }

    [Fact]
    public async Task SummaryWithNoMessagesStillReturnsContext()
    {
        var conversation = await SeedAsync(4);
        await database.ChatMessages.ExecuteDeleteAsync();
        var context = await reader.ReadAsync(conversation.SessionId, conversation.Id, "question");
        Assert.NotNull(context);
        Assert.Equal("Saved summary", context.ConversationSummary);
        Assert.Empty(context.RecentMessages);
    }

    [Fact]
    public async Task SequenceIsUniqueWithinEachConversation()
    {
        var conversation = await SeedAsync(2);
        database.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id,
            Sequence = 2, Role = "user", Content = "Duplicate sequence"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }

    private async Task<Conversation> SeedAsync(long? boundary)
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Session = new AnonymousSession { Id = Guid.NewGuid(), TokenHash = "test-hash" },
            Summary = boundary is { } summaryBoundary
                ? new ConversationSummary { Content = "Saved summary", LastSummarizedSequence = summaryBoundary }
                : null
        };
        // Deliberately give timestamps the opposite order to the authoritative sequence.
        for (var sequence = 4; sequence >= 1; sequence--)
            conversation.ChatMessages.Add(new ChatMessage
            {
                Id = Guid.NewGuid(), Sequence = sequence,
                Role = sequence % 2 == 1 ? "user" : "assistant", Content = $"Message {sequence}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-sequence)
            });
        database.Conversations.Add(conversation);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        return conversation;
    }
}
