using ImAI.Domain.Entities;
using ImAI.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ImAI.Api.Tests;

public sealed class ConversationSummaryTests
{
    [Fact]
    public async Task MigrationCreatesSummaryAndRelationshipCanBeLoaded()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ImAiDbContext>().UseSqlite(connection).Options;
        await using var database = new ImAiDbContext(options);
        await database.Database.MigrateAsync();
        var summary = new ConversationSummary { Content = "Conversation context", LastSummarizedSequence = 12 };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Session = new AnonymousSession { Id = Guid.NewGuid(), TokenHash = "test", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(7) },
            Summary = summary
        };
        database.Conversations.Add(conversation);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var loaded = await database.Conversations.Include(c => c.Summary).SingleAsync();
        Assert.NotNull(loaded.Summary);
        Assert.Equal(summary.Id, loaded.Summary.Id);
        Assert.Equal(loaded.Id, loaded.Summary.ConversationId);
        Assert.Same(loaded, loaded.Summary.Conversation);
        Assert.Equal("Conversation context", loaded.Summary.Content);
        Assert.Equal(12, loaded.Summary.LastSummarizedSequence);
        Assert.Equal(1, loaded.Summary.Version);
    }

    [Fact]
    public async Task ForeignKeyAndUniqueConversationConstraintAreEnforced()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ImAiDbContext>().UseSqlite(connection).Options;
        await using var database = new ImAiDbContext(options);
        await database.Database.MigrateAsync();
        database.ConversationSummaries.Add(new ConversationSummary { ConversationId = Guid.NewGuid() });
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        database.ChangeTracker.Clear();
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Session = new AnonymousSession { Id = Guid.NewGuid(), TokenHash = "test" },
            Summary = new ConversationSummary()
        };
        database.Conversations.Add(conversation);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        database.ConversationSummaries.Add(new ConversationSummary { ConversationId = conversation.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
    }
}
