namespace ImAI.Domain.Entities;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AnonymousSession Session { get; set; } = null!;
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
