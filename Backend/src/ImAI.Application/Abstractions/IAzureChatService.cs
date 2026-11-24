using ImAI.Application.Dtos;

namespace ImAI.Application.Abstractions;

public interface IAzureChatService
{
    Task<string> AskAsync(ConversationContextDto context, CancellationToken cancellationToken = default);
}
