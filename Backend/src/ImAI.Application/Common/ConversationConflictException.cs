namespace ImAI.Application.Common;

public sealed class ConversationConflictException() : Exception("This conversation changed while generating the reply. Retry the prompt.");
