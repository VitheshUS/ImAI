using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using MediatR;

namespace ImAI.Application.Chat;

public sealed record SendChatQuery(string Query, Guid? ConversationId = null) : IRequest<AiChatResponse>;

public sealed class SendChatQueryHandler(IAzureChatService chatService) : IRequestHandler<SendChatQuery, AiChatResponse>
{
    public async Task<AiChatResponse> Handle(SendChatQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new RequestValidationException("Query is required.");
        if (request.ConversationId == Guid.Empty)
            throw new RequestValidationException("ConversationId must be a non-empty UUID when supplied.");

        var result = await chatService.AskAsync(request.Query, cancellationToken);
        return new(request.ConversationId ?? Guid.NewGuid(), Guid.NewGuid(), result, []);
    }
}
