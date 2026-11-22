using ImAI.Application.Abstractions;
using Microsoft.Extensions.AI;

namespace ImAI.Infrastructure.Chat;

public sealed class AzureChatService(IChatClient chatClient) : IAzureChatService
{
    public async Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, "You are ImAI, a helpful AI assistant."),
             new ChatMessage(ChatRole.User, question)],
            cancellationToken: cancellationToken);
        return response.Text;
    }
}
