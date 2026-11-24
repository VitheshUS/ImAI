using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using ImAI.Application.Configuration;
using ImAI.Application.Dtos;
using ImAI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Options;

namespace ImAI.Application.Chat;

public sealed record SendChatQuery(Guid SessionId, string Query, Guid? ConversationId = null) : IRequest<AiChatResponse>;

public sealed class SendChatQueryHandler(
    IAzureChatService chatService,
    IConversationStore conversations,
    IConversationContextReader contextReader,
    IOptions<ConversationContextOptions> options,
    TimeProvider clock)
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

        var history = conversation.ChatMessages.OrderBy(message => message.Sequence).ToArray();
        var userSequence = checked((history.Length == 0 ? 0 : history[^1].Sequence) + 1);
        var userCreatedAt = history.Length == 0 || now > history[^1].CreatedAt ? now : history[^1].CreatedAt.AddTicks(1);
        var context = await contextReader.ReadAsync(request.SessionId, conversation.Id, request.Query, cancellationToken)
            ?? new ConversationContextDto(options.Value.SystemInstructions, string.Empty, [], [], request.Query);
        var result = await chatService.AskAsync(context, cancellationToken);
        var replyTime = clock.GetUtcNow().UtcDateTime;
        var assistantCreatedAt = replyTime > userCreatedAt ? replyTime : userCreatedAt.AddTicks(1);
        var assistantMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Sequence = checked(userSequence + 1),
            Role = "assistant",
            Content = result,
            CreatedAt = assistantCreatedAt
        };
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            Sequence = userSequence,
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
