using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using Azure.Identity;
using ImAI.Application.Chat;
using Microsoft.Extensions.AI;

namespace ImAI.Infrastructure.Chat;

public sealed class AzureChatService(IChatClient chatClient) : IAzureChatService
{
    public async Task<string> AskAsync(IReadOnlyList<ChatTurn> messages, CancellationToken cancellationToken = default)
    {
        var history = new List<ChatMessage> { new(ChatRole.System, "You are ImAI, a helpful AI assistant.") };
        history.AddRange(messages.Select(message => new ChatMessage(message.Role switch
        {
            "user" => ChatRole.User,
            "assistant" => ChatRole.Assistant,
            _ => throw new InvalidOperationException("Unsupported conversation message role.")
        }, message.Content)));
        try
        {
            var response = await chatClient.GetResponseAsync(history, cancellationToken: cancellationToken);
            return response.Text;
        }
        catch (AuthenticationFailedException exception)
        {
            throw new AiServiceUnavailableException("AI service authentication is not configured on the server.", exception);
        }
    }
}
