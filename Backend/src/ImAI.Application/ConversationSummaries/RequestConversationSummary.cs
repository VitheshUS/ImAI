using ImAI.Application.Abstractions.Messaging;
using ImAI.Application.Common;
using ImAI.Application.Dtos.Events;
using MediatR;

namespace ImAI.Application.ConversationSummaries;

public sealed record RequestConversationSummary(
    Guid ConversationId,
    Guid SessionId,
    long LastSummarizedSequence) : IRequest<bool>;

public sealed class RequestConversationSummaryHandler(IConversationSummaryEventPublisher publisher)
    : IRequestHandler<RequestConversationSummary, bool>
{
    public Task<bool> Handle(RequestConversationSummary request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.ConversationId == Guid.Empty)
            throw new RequestValidationException("ConversationId must be a non-empty UUID.");
        if (request.SessionId == Guid.Empty)
            throw new RequestValidationException("SessionId must be a non-empty UUID.");
        if (request.LastSummarizedSequence < 0)
            throw new RequestValidationException("LastSummarizedSequence must be zero or greater.");

        var summaryEvent = new ConversationSummaryRequestedEvent(
            request.ConversationId, request.SessionId, request.LastSummarizedSequence);
        return Task.FromResult(publisher.TryPublish(summaryEvent));
    }
}
