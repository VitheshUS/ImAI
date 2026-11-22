namespace ImAI.Application.Chat;

public sealed record AiChatResponse(Guid ConversationId, Guid MessageId, string Result, string[] Source);
