using System.Collections.Concurrent;
using System.Threading.Channels;
using ImAI.Application.Abstractions.Messaging;
using ImAI.Application.Dtos.Events;
using ImAI.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ImAI.Api.Tests;

public sealed class ConversationSummaryChannelTests
{
    [Fact]
    public void QueueIsSharedAcrossRequestScopesAndPreservesAcceptedEventsWhenFull()
    {
        using var provider = CreateProvider("2");
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var firstPublisher = firstScope.ServiceProvider.GetRequiredService<IConversationSummaryEventPublisher>();
        var secondPublisher = secondScope.ServiceProvider.GetRequiredService<IConversationSummaryEventPublisher>();
        var reader = provider.GetRequiredService<ChannelReader<ConversationSummaryRequestedEvent>>();
        Assert.Same(firstPublisher, secondPublisher);
        Assert.Same(reader, secondScope.ServiceProvider.GetRequiredService<ChannelReader<ConversationSummaryRequestedEvent>>());
        var first = CreateEvent();
        var second = CreateEvent();
        var third = CreateEvent();

        Assert.True(firstPublisher.TryPublish(first));
        Assert.True(secondPublisher.TryPublish(second));
        Assert.False(firstPublisher.TryPublish(third));
        Assert.True(reader.TryRead(out var readFirst));
        Assert.Equal(first, readFirst);
        Assert.True(secondPublisher.TryPublish(third));
        Assert.True(reader.TryRead(out var readSecond));
        Assert.Equal(second, readSecond);
        Assert.True(reader.TryRead(out var readThird));
        Assert.Equal(third, readThird);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public async Task MultipleWritersFeedOneReaderWithoutLosingAcceptedEvents()
    {
        using var provider = CreateProvider("8");
        var publisher = provider.GetRequiredService<IConversationSummaryEventPublisher>();
        var channel = provider.GetRequiredService<Channel<ConversationSummaryRequestedEvent>>();
        var reader = provider.GetRequiredService<ChannelReader<ConversationSummaryRequestedEvent>>();
        var accepted = new ConcurrentBag<ConversationSummaryRequestedEvent>();
        var received = new List<ConversationSummaryRequestedEvent>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var consume = Task.Run(async () =>
        {
            await foreach (var summaryEvent in reader.ReadAllAsync(timeout.Token)) received.Add(summaryEvent);
        }, timeout.Token);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                for (var index = 0; index < 100; index++)
                {
                    var summaryEvent = CreateEvent();
                    if (publisher.TryPublish(summaryEvent)) accepted.Add(summaryEvent);
                }
            }, timeout.Token)));
        }
        finally
        {
            channel.Writer.TryComplete();
        }
        await consume;
        Assert.NotEmpty(accepted);
        Assert.Equal(accepted.Count, received.Count);
        Assert.True(accepted.ToHashSet().SetEquals(received));
    }

    [Fact]
    public void CompletedQueueRejectsPublicationWithoutThrowing()
    {
        using var provider = CreateProvider("1");
        provider.GetRequiredService<Channel<ConversationSummaryRequestedEvent>>().Writer.TryComplete();
        Assert.False(provider.GetRequiredService<IConversationSummaryEventPublisher>().TryPublish(CreateEvent()));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void InvalidCapacityIsRejected(string capacity)
    {
        using var provider = CreateProvider(capacity);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IConversationSummaryEventPublisher>());
    }

    [Fact]
    public void NonNumericCapacityIsRejected()
    {
        using var provider = CreateProvider("invalid");
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IConversationSummaryEventPublisher>());
    }

    private static ConversationSummaryRequestedEvent CreateEvent() => new(Guid.NewGuid(), Guid.NewGuid(), 0);

    private static ServiceProvider CreateProvider(string capacity) => new ServiceCollection()
        .AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConversationSummary:Queue:Capacity"] = capacity
        }).Build()).BuildServiceProvider();
}
