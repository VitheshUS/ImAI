using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using MediatR;

namespace ImAI.Application.Chat;

public sealed record ConversationOverview(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record StoredChatMessage(Guid Id, string Role, string Content, DateTime CreatedAt);
public sealed record ConversationDetails(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<StoredChatMessage> Messages);
public sealed record ListConversations(Guid SessionId) : IRequest<IReadOnlyList<ConversationOverview>>;
public sealed record GetConversation(Guid SessionId, Guid ConversationId) : IRequest<ConversationDetails>;
public sealed record DeleteConversation(Guid SessionId, Guid ConversationId) : IRequest;

public sealed class ListConversationsHandler(IConversationStore store) : IRequestHandler<ListConversations, IReadOnlyList<ConversationOverview>>
{
    public Task<IReadOnlyList<ConversationOverview>> Handle(ListConversations request, CancellationToken cancellationToken) =>
        store.ListAsync(request.SessionId, cancellationToken);
}

public sealed class GetConversationHandler(IConversationStore store) : IRequestHandler<GetConversation, ConversationDetails>
{
    public async Task<ConversationDetails> Handle(GetConversation request, CancellationToken cancellationToken)
    {
        var conversation = await store.FindAsync(request.SessionId, request.ConversationId, cancellationToken)
            ?? throw new ConversationNotFoundException();
        var messages = conversation.ChatMessages.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new StoredChatMessage(m.Id, m.Role, m.Content, m.CreatedAt)).ToArray();
        return new(conversation.Id, messages.FirstOrDefault(m => m.Role == "user")?.Content ?? "New conversation",
            conversation.CreatedAt, conversation.UpdatedAt, messages);
    }
}

public sealed class DeleteConversationHandler(IConversationStore store) : IRequestHandler<DeleteConversation>
{
    public async Task Handle(DeleteConversation request, CancellationToken cancellationToken)
    {
        if (!await store.DeleteAsync(request.SessionId, request.ConversationId, cancellationToken))
            throw new ConversationNotFoundException();
    }
}
