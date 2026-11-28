using ImAI.Application.DependencyInjection;
using ImAI.Application.Abstractions.Messaging;
using ImAI.Application.Common;
using ImAI.Application.ConversationSummaries;
using ImAI.Application.Dtos.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ImAI.Api.Tests;

public sealed class RequestConversationSummaryTests
{
    [Theory]
    [InlineData(true, 0L)]
    [InlineData(false, 0L)]
    [InlineData(true, 12L)]
    [InlineData(false, 12L)]
    public async Task PublishesTheSummaryCheckpointAndReturnsAcceptanceThroughMediatr(bool accepted, long sequence)
    {
        var publisher = new RecordingPublisher(accepted);
        var services = new ServiceCollection().AddApplication();
        services.AddSingleton<IConversationSummaryEventPublisher>(publisher);
        using var provider = services.BuildServiceProvider();
        var request = new RequestConversationSummary(Guid.NewGuid(), Guid.NewGuid(), sequence);

        var result = await provider.GetRequiredService<ISender>().Send(request);

        Assert.Equal(accepted, result);
        Assert.Equal(new ConversationSummaryRequestedEvent(request.ConversationId, request.SessionId, sequence), Assert.Single(publisher.Events));
    }

    [Theory]
    [InlineData(true, false, 12)]
    [InlineData(false, true, 12)]
    [InlineData(false, false, -1)]
    public async Task InvalidRequestIsRejectedBeforePublishing(bool emptyConversation, bool emptySession, long sequence)
    {
        var publisher = new RecordingPublisher(true);
        var handler = new RequestConversationSummaryHandler(publisher);
        var request = new RequestConversationSummary(
            emptyConversation ? Guid.Empty : Guid.NewGuid(), emptySession ? Guid.Empty : Guid.NewGuid(), sequence);

        await Assert.ThrowsAsync<RequestValidationException>(async () => await handler.Handle(request, CancellationToken.None));
        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task CancelledRequestIsNotPublished()
    {
        var publisher = new RecordingPublisher(true);
        var handler = new RequestConversationSummaryHandler(publisher);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await handler.Handle(
            new RequestConversationSummary(Guid.NewGuid(), Guid.NewGuid(), 12), cancellation.Token));
        Assert.Empty(publisher.Events);
    }

    private sealed class RecordingPublisher(bool accepted) : IConversationSummaryEventPublisher
    {
        public List<ConversationSummaryRequestedEvent> Events { get; } = [];

        public bool TryPublish(ConversationSummaryRequestedEvent summaryEvent)
        {
            Events.Add(summaryEvent);
            return accepted;
        }
    }
}
