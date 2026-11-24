namespace ImAI.Application.Configuration;

public sealed class ConversationContextOptions
{
    public const string SectionName = "ConversationContext";

    public string SystemInstructions { get; set; } = string.Empty;
}
