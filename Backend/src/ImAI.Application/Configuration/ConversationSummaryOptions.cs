namespace ImAI.Application.Configuration;

public sealed class ConversationSummaryOptions
{
    public const string SectionName = "ConversationSummary";

    public long KLimit { get; set; } = 10;
}
