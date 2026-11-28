using System.Globalization;
using System.Threading.Channels;
using ImAI.Application.Abstractions.Messaging;
using ImAI.Application.Dtos.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ImAI.Infrastructure.Messaging.ConversationSummaries;

namespace ImAI.Infrastructure.DependencyInjection;

internal static class ConversationSummaryMessagingExtensions
{
    public static IServiceCollection AddConversationSummaryMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ConversationSummaryQueueOptions>()
            .Configure(options =>
            {
                var value = configuration[$"{ConversationSummaryQueueOptions.SectionName}:Capacity"];
                if (value is null) return;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity))
                    throw new InvalidOperationException("ConversationSummary:Queue:Capacity must be a positive integer.");
                options.Capacity = capacity;
            })
            .Validate(options => options.Capacity > 0, "ConversationSummary:Queue:Capacity must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<Channel<ConversationSummaryRequestedEvent>>(provider =>
        {
            var capacity = provider.GetRequiredService<IOptions<ConversationSummaryQueueOptions>>().Value.Capacity;
            return Channel.CreateBounded<ConversationSummaryRequestedEvent>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                // TryWrite returns false immediately when full. DropWrite would report
                // success even for a dropped event, breaking TryPublish's acceptance result.
                FullMode = BoundedChannelFullMode.Wait
            });
        });
        services.AddSingleton<ChannelReader<ConversationSummaryRequestedEvent>>(provider =>
            provider.GetRequiredService<Channel<ConversationSummaryRequestedEvent>>().Reader);
        services.AddSingleton<IConversationSummaryEventPublisher>(provider =>
            new ChannelConversationSummaryEventPublisher(provider.GetRequiredService<Channel<ConversationSummaryRequestedEvent>>().Writer));
        return services;
    }
}
