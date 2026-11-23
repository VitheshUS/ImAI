using ImAI.Application.Chat;

namespace ImAI.Application.Abstractions;

public interface IAzureChatService
{
    Task<string> AskAsync(IReadOnlyList<ChatTurn> messages, CancellationToken cancellationToken = default);
}
