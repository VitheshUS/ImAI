using ImAI.Application.Dtos;

namespace ImAI.Application.Abstractions;

public interface IConversationContextReader
{
    /// <summary>
    /// Reads the summary and messages after its sequence boundary for a session-owned conversation.
    /// Returns null when no summary exists or the conversation does not belong to the session.
    /// </summary>
    Task<ConversationContextDto?> ReadAsync(
        Guid sessionId,
        Guid conversationId,
        string currentMessage,
        CancellationToken cancellationToken = default);
}
