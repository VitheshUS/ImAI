using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using MediatR;
namespace ImAI.Application.Chat;

public sealed record SendChatQuery(string Query, Guid? ConversationId = null) : IRequest<ChatReply>;
public sealed record ChatReply(Guid ConversationId, string Answer, bool IsSimulated, string[] Sources);
public sealed class SendChatQueryHandler(IChatResponder responder) : IRequestHandler<SendChatQuery, ChatReply>
{
    public async Task<ChatReply> Handle(SendChatQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 8000)
            throw new RequestValidationException("Query must contain between 1 and 8000 characters.");
        var answer = await responder.RespondAsync(request.Query.Trim(), cancellationToken);
        return new(request.ConversationId ?? Guid.NewGuid(), answer, true, []);
    }
}
