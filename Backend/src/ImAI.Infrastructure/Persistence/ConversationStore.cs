using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using ImAI.Application.Chat;
using ImAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImAI.Infrastructure.Persistence;

internal sealed class ConversationStore(ImAiDbContext database) : IConversationStore
{
    public async Task<IReadOnlyList<ConversationOverview>> ListAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await database.Conversations.AsNoTracking().Where(c => c.SessionId == sessionId)
            .OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id)
            .Select(c => new ConversationOverview(c.Id,
                c.ChatMessages.Where(m => m.Role == "user").OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
                    .Select(m => m.Content).FirstOrDefault() ?? "New conversation", c.CreatedAt, c.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> DeleteAsync(Guid sessionId, Guid conversationId, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        if (!await database.Conversations.AnyAsync(c => c.Id == conversationId && c.SessionId == sessionId, cancellationToken))
            return false;
        await database.ConversationSummaries.Where(s => s.ConversationId == conversationId).ExecuteDeleteAsync(cancellationToken);
        await database.ChatMessages.Where(m => m.ConversationId == conversationId).ExecuteDeleteAsync(cancellationToken);
        await database.Conversations.Where(c => c.Id == conversationId && c.SessionId == sessionId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<Conversation?> FindAsync(Guid sessionId, Guid conversationId, CancellationToken cancellationToken) =>
        database.Conversations.Include(conversation => conversation.ChatMessages)
            .SingleOrDefaultAsync(conversation => conversation.Id == conversationId && conversation.SessionId == sessionId, cancellationToken);

    public async Task SaveExchangeAsync(Conversation conversation, ChatMessage userMessage, ChatMessage assistantMessage, CancellationToken cancellationToken)
    {
        if (database.Entry(conversation).State == EntityState.Detached)
            database.Conversations.Add(conversation);
        database.ChatMessages.AddRange(userMessage, assistantMessage);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConversationConflictException();
        }
    }
}
