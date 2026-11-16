using ImAI.Application.Abstractions;
namespace ImAI.Infrastructure.Chat;

internal sealed class DummyChatResponder : IChatResponder
{
    public Task<string> RespondAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult($"ImAI received your query: {query}\nThis is a dummy response. Retrieval and AI generation are not enabled in Phase 2.");
    }
}
