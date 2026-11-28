using ImAI.Application.Dtos.Events;

namespace ImAI.Application.Abstractions.Messaging;

/// <summary>Application port for publishing conversation summary generation requests.</summary>
public interface IConversationSummaryEventPublisher
{
    /// <summary>
    /// Attempts to publish without waiting for summary generation.
    /// Returns true when accepted for processing, or false when it cannot be accepted.
    /// Acceptance does not mean the summary has been generated or durably stored.
    /// </summary>
    bool TryPublish(ConversationSummaryRequestedEvent summaryEvent);
}
