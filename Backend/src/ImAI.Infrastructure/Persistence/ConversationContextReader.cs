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
    public Task<ConversationContextDto?> ReadAsync(
        Guid sessionId,
        Guid conversationId,
        string currentMessage,
        CancellationToken cancellationToken = default)
    {
        var systemInstructions = options.Value.SystemInstructions;
        return database.ConversationSummaries.AsNoTracking()
            .Where(summary => summary.ConversationId == conversationId && summary.Conversation.SessionId == sessionId)
            .Select(summary => new ConversationContextDto(
                systemInstructions,
                summary.Content,
                summary.Conversation.ChatMessages
                    .Where(message => message.Sequence > summary.LastSummarizedSequence)
                    .OrderBy(message => message.Sequence)
                    .Select(message => new ContextMessageDto(message.Role, message.Content))
                    .ToList(),
                Array.Empty<string>(),
                currentMessage))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
