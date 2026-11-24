using ImAI.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ImAI.Api.Tests;

public sealed class ChatMessageSequenceMigrationTests
{
    [Fact]
    public async Task MigrationNumbersExistingMessagesWithoutLosingHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new ImAiDbContext(new DbContextOptionsBuilder<ImAiDbContext>().UseSqlite(connection).Options);
        await database.GetService<IMigrator>().MigrateAsync("20261123164316_CreateConversationSummary");
        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "AnonymousSession" ("Id", "TokenHash", "CreatedAt", "ExpiresAt")
            VALUES ({sessionId}, {"test-hash"}, {now}, {now.AddDays(7)})
            """);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        foreach (var id in new[] { firstId, secondId })
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Conversation" ("Id", "SessionId", "CreatedAt", "UpdatedAt")
                VALUES ({id}, {sessionId}, {now}, {now})
                """);
        var messages = new[]
        {
            (Guid.Parse("00000000-0000-0000-0000-000000000003"), firstId, "late", now),
            (Guid.Parse("00000000-0000-0000-0000-000000000002"), firstId, "middle", now),
            (Guid.Parse("00000000-0000-0000-0000-000000000001"), firstId, "early", now.AddMinutes(-1)),
            (Guid.NewGuid(), secondId, "other conversation", now)
        };
        foreach (var (id, conversationId, content, createdAt) in messages)
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ChatMessage" ("Id", "ConversationId", "Role", "Content", "CreatedAt")
                VALUES ({id}, {conversationId}, {"user"}, {content}, {createdAt})
                """);

        await database.Database.MigrateAsync();

        var first = await database.ChatMessages.Where(message => message.ConversationId == firstId)
            .OrderBy(message => message.Sequence).ToListAsync();
        Assert.Equal(new[] { "early", "middle", "late" }, first.Select(message => message.Content));
        Assert.Equal(new long[] { 1, 2, 3 }, first.Select(message => message.Sequence));
        Assert.Equal(1, (await database.ChatMessages.SingleAsync(message => message.ConversationId == secondId)).Sequence);
        Assert.Equal(messages.Length, await database.ChatMessages.CountAsync());
    }
}
