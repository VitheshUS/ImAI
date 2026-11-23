using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using ImAI.Domain.Entities;
using MediatR;

namespace ImAI.Application.Chat;

public sealed record SendChatQuery(Guid SessionId, string Query, Guid? ConversationId = null) : IRequest<AiChatResponse>;

public sealed class SendChatQueryHandler(IAzureChatService chatService, IConversationStore conversations, TimeProvider clock)
    : IRequestHandler<SendChatQuery, AiChatResponse>
{
    public async Task<AiChatResponse> Handle(SendChatQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new RequestValidationException("Query is required.");
        if (request.ConversationId == Guid.Empty)
            throw new RequestValidationException("ConversationId must be a non-empty UUID when supplied.");
        if (request.SessionId == Guid.Empty)
            throw new InvalidOperationException("An authenticated session is required.");

        var now = clock.GetUtcNow().UtcDateTime;
        var conversation = request.ConversationId is { } conversationId
            ? await conversations.FindAsync(request.SessionId, conversationId, cancellationToken)
                ?? throw new ConversationNotFoundException()
            : new Conversation { Id = Guid.NewGuid(), SessionId = request.SessionId, CreatedAt = now, UpdatedAt = now };

        var history = conversation.ChatMessages.OrderBy(message => message.CreatedAt).ThenBy(message => message.Id).ToArray();
        var userCreatedAt = history.Length == 0 || now > history[^1].CreatedAt ? now : history[^1].CreatedAt.AddTicks(1);
        // Persist history, but only send the current prompt to AI in this phase.
        var result = await chatService.AskAsync([new ChatTurn("user", request.Query)], cancellationToken);
        var replyTime = clock.GetUtcNow().UtcDateTime;
        var assistantCreatedAt = replyTime > userCreatedAt ? replyTime : userCreatedAt.AddTicks(1);
        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = result,
            CreatedAt = assistantCreatedAt
        };
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Role = "user",
            Content = request.Query,
            CreatedAt = userCreatedAt
        };
        conversation.UpdatedAt = assistantCreatedAt;
        // Persist both messages together, only after generation succeeds.
        await conversations.SaveExchangeAsync(conversation, userMessage, assistantMessage, cancellationToken);
        return new(conversation.Id, assistantMessage.Id, result, []);
    }
}
