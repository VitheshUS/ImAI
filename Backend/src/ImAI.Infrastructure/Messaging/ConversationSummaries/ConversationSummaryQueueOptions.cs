namespace ImAI.Infrastructure.Messaging.ConversationSummaries;

internal sealed class ConversationSummaryQueueOptions
{
    public const string SectionName = "ConversationSummary:Queue";

    public int Capacity { get; set; } = 100;
}
