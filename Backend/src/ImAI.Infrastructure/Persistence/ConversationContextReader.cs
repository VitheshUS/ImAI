using ImAI.Application.Abstractions;
using ImAI.Application.Configuration;
using ImAI.Application.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ImAI.Infrastructure.Persistence;

internal sealed class ConversationContextReader(
    ImAiDbContext database,
    IOptions<ConversationContextOptions> options) : IConversationContextReader
{
    public async Task<ConversationContextDto?> ReadAsync(
        Guid sessionId,
        Guid conversationId,
        string currentMessage,
        CancellationToken cancellationToken = default)
    {
        // Resolve the optional summary separately: correlating it inside a collection
        // projection requires SQL APPLY, which SQLite does not support.
        var conversation = await database.Conversations.AsNoTracking()
            .Where(conversation => conversation.Id == conversationId && conversation.SessionId == sessionId)
            .Select(conversation => new
            {
                Summary = conversation.Summary == null ? string.Empty : conversation.Summary.Content,
                LastSummarizedSequence = conversation.Summary == null ? 0L : conversation.Summary.LastSummarizedSequence
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (conversation is null) return null;

        var recentMessages = await database.ChatMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId && message.Conversation.SessionId == sessionId
                && message.Sequence > conversation.LastSummarizedSequence)
            .OrderBy(message => message.Sequence)
            .Select(message => new ContextMessageDto(message.Role, message.Content))
            .ToListAsync(cancellationToken);
        return new ConversationContextDto(options.Value.SystemInstructions, conversation.Summary, recentMessages, [], currentMessage);
    }
}
