using ImAI.Domain.Entities;
using ImAI.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImAI.Api.Tests;

public sealed class ChatRelationshipTests
{
    [Fact]
    public async Task SessionLoadsMultipleConversationsAndTheirMessages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ImAiDbContext>().UseSqlite(connection).Options;
        await using var context = new ImAiDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var session = new AnonymousSession
        {
            Id = Guid.NewGuid(),
            TokenHash = "test-hash",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        for (var i = 0; i < 2; i++)
        {
            var conversation = new Conversation { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            conversation.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), Sequence = 1, Role = "user", Content = "Hello", CreatedAt = DateTime.UtcNow });
            conversation.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), Sequence = 2, Role = "assistant", Content = "Hi", CreatedAt = DateTime.UtcNow });
            session.Conversations.Add(conversation);
        }
        context.AnonymousSessions.Add(session);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var loaded = await context.AnonymousSessions.Include(s => s.Conversations)
            .ThenInclude(c => c.ChatMessages).SingleAsync();
        Assert.Equal(2, loaded.Conversations.Count);
        Assert.All(loaded.Conversations, conversation =>
        {
            Assert.Equal(loaded.Id, conversation.SessionId);
            Assert.Same(loaded, conversation.Session);
            Assert.Equal(2, conversation.ChatMessages.Count);
            Assert.All(conversation.ChatMessages, message =>
            {
                Assert.Equal(conversation.Id, message.ConversationId);
                Assert.Same(conversation, message.Conversation);
            });
        });
    }

    [Fact]
    public async Task ForeignKeysRejectOrphanConversationsAndMessages()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ImAiDbContext>().UseSqlite(connection).Options;
        await using var context = new ImAiDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Conversations.Add(new Conversation { Id = Guid.NewGuid(), SessionId = Guid.NewGuid() });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        context.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = Guid.NewGuid(), Role = "user", Content = "hello" });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
