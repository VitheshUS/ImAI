using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using ImAI.Application.Configuration;
using ImAI.Application.Dtos;
using ImAI.Application.ConversationSummaries;
using ImAI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ImAI.Application.Chat;

public sealed record SendChatQuery(Guid SessionId, string Query, Guid? ConversationId = null) : IRequest<AiChatResponse>;

public sealed class SendChatQueryHandler(
    IAzureChatService chatService,
    IConversationStore conversations,
    IConversationContextReader contextReader,
    IOptions<ConversationContextOptions> options,
    IOptions<ConversationSummaryOptions> summaryOptions,
    TimeProvider clock,
    ILogger<SendChatQueryHandler> logger,
    ISender sender)
    : IRequestHandler<SendChatQuery, AiChatResponse>
{
    public async Task<AiChatResponse> Handle(SendChatQuery request, CancellationToken cancellationToken)
    {
        using var operationScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ChatOperationId"] = Guid.NewGuid(),
            ["SessionId"] = request.SessionId
        });
        var started = Stopwatch.GetTimestamp();
        logger.LogInformation(new EventId(2000, "ChatRequestReceived"),
            "Chat request received for {RequestedConversationId}: {Query}", request.ConversationId, request.Query);
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

        using var conversationScope = logger.BeginScope(new Dictionary<string, object?> { ["ConversationId"] = conversation.Id });
        logger.LogInformation(new EventId(2001, "ConversationResolved"),
            "Conversation resolved; new conversation: {IsNewConversation}", request.ConversationId is null);
        var history = conversation.ChatMessages.OrderBy(message => message.Sequence).ToArray();
        var userSequence = checked((history.Length == 0 ? 0 : history[^1].Sequence) + 1);
        var userCreatedAt = history.Length == 0 || now > history[^1].CreatedAt ? now : history[^1].CreatedAt.AddTicks(1);
        var context = request.ConversationId is null
            ? new ConversationContextDto(options.Value.SystemInstructions, string.Empty, [], [], request.Query)
            : await contextReader.ReadAsync(request.SessionId, conversation.Id, request.Query, cancellationToken)
                ?? throw new ConversationNotFoundException();
        logger.LogInformation(new EventId(2002, "ChatContextConstructed"), "Chat context constructed: {@Context}", context);
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
        logger.LogInformation(new EventId(2003, "ChatExchangeSaved"),
            "Chat exchange saved: user message {UserMessageId} sequence {UserSequence}, assistant message {MessageId} sequence {AssistantSequence}; completed in {ElapsedMilliseconds} ms",
            userMessage.Id, userMessage.Sequence, assistantMessage.Id, assistantMessage.Sequence,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        var lastSummarizedSequence = conversation.Summary?.LastSummarizedSequence ?? 0;
        var difference = assistantMessage.Sequence - lastSummarizedSequence;
        if (difference > summaryOptions.Value.KLimit)
        {
            try
            {
                // This exchange is committed; a client disconnect should not cancel its publication.
                var accepted = await sender.Send(new RequestConversationSummary(
                    conversation.Id, request.SessionId, lastSummarizedSequence), CancellationToken.None);
                logger.Log(accepted ? LogLevel.Information : LogLevel.Warning,
                    new EventId(2004, "ConversationSummaryPublishAttempt"),
                    "Summary request publication accepted: {Accepted}; current sequence {CurrentSequence}, last summarized sequence {LastSummarizedSequence}, difference {SequenceDifference}, K limit {KLimit}",
                    accepted, assistantMessage.Sequence, lastSummarizedSequence, difference, summaryOptions.Value.KLimit);
            }
            catch (Exception exception)
            {
                // Background publication must not fail a chat whose messages are already saved.
                logger.LogWarning(new EventId(2005, "ConversationSummaryPublishFailed"), exception,
                    "Summary request publication failed for last summarized sequence {LastSummarizedSequence}; the chat exchange is saved",
                    lastSummarizedSequence);
            }
        }
        return new(conversation.Id, assistantMessage.Id, result, []);
    }
}
