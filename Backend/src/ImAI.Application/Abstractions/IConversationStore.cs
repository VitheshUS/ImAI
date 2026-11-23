using ImAI.Domain.Entities;
using ImAI.Application.Chat;

namespace ImAI.Application.Abstractions;

public interface IConversationStore
{
    Task<IReadOnlyList<ConversationOverview>> ListAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid sessionId, Guid conversationId, CancellationToken cancellationToken);
    Task<Conversation?> FindAsync(Guid sessionId, Guid conversationId, CancellationToken cancellationToken);
    Task SaveExchangeAsync(Conversation conversation, ChatMessage userMessage, ChatMessage assistantMessage, CancellationToken cancellationToken);
}
