using ImAI.Application.Abstractions;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace ImAI.Infrastructure.Chat;

public sealed class AzureChatService : IAzureChatService
{
    private readonly IChatClient _chatClient;

    public AzureChatService(
        string endpoint,
        string deploymentName)
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new DefaultAzureCredential());

        _chatClient = azureClient
            .GetChatClient(deploymentName)
            .AsIChatClient();
    }

    public async Task<string> AskAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var response = await _chatClient.GetResponseAsync(
            [
                new ChatMessage(
                    ChatRole.System,
                    "You are ImAI, a helpful AI assistant."),
                new ChatMessage(ChatRole.User, question)
            ],
            cancellationToken: cancellationToken);

        return response.Text ?? string.Empty;
    }
}