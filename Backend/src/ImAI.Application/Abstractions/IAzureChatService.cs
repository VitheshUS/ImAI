namespace ImAI.Application.Abstractions;

public interface IAzureChatService
{
    Task<string> AskAsync(string question, CancellationToken cancellationToken = default);
}
