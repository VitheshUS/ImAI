namespace ImAI.Application.Dtos.Events;

/// <summary>
/// Requests summary generation for persisted messages after LastSummarizedSequence.
/// A zero sequence means the conversation has not been summarized yet.
/// </summary>
public sealed record ConversationSummaryRequestedEvent(
    Guid ConversationId,
    Guid SessionId,
    long LastSummarizedSequence);
