namespace ImAI.Domain.Entities;

public sealed class AnonymousSession
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}
