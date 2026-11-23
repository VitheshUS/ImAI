namespace ImAI.Domain.Entities;

public sealed class ConversationSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public long LastSummarizedSequence { get; set; }
    public int Version { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
