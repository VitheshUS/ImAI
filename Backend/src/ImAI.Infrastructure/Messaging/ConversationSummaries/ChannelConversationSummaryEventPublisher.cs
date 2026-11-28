using System.Threading.Channels;
using ImAI.Application.Abstractions.Messaging;
using ImAI.Application.Dtos.Events;

namespace ImAI.Infrastructure.Messaging.ConversationSummaries;

internal sealed class ChannelConversationSummaryEventPublisher(
    ChannelWriter<ConversationSummaryRequestedEvent> writer) : IConversationSummaryEventPublisher
{
    public bool TryPublish(ConversationSummaryRequestedEvent summaryEvent)
    {
        ArgumentNullException.ThrowIfNull(summaryEvent);
        return writer.TryWrite(summaryEvent);
    }
}
