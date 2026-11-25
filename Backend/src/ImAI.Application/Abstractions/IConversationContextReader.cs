using ImAI.Application.Dtos;

namespace ImAI.Application.Abstractions;

public interface IConversationContextReader
{
    /// <summary>
    /// Reads a session-owned conversation's optional summary and messages after its sequence boundary.
    /// Without a summary, returns an empty summary and all messages after sequence zero.
    /// Returns null only when the conversation is unknown or does not belong to the session.
    /// </summary>
    Task<ConversationContextDto?> ReadAsync(
        Guid sessionId,
        Guid conversationId,
        string currentMessage,
        CancellationToken cancellationToken = default);
}
