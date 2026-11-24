namespace ImAI.Application.Dtos;

public sealed record ConversationContextDto(
    string SystemInstructions,
    string ConversationSummary,
    IReadOnlyList<ContextMessageDto> RecentMessages,
    IReadOnlyList<string> RetrievedKnowledge,
    string CurrentMessage);
