using ImAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImAI.Infrastructure.Persistence;

public sealed class ImAiDbContext(DbContextOptions<ImAiDbContext> options) : DbContext(options)
{
    public DbSet<AnonymousSession> AnonymousSessions => Set<AnonymousSession>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnonymousSession>(entity =>
        {
            entity.ToTable("AnonymousSession");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.TokenHash).IsRequired();
            entity.HasIndex(session => session.TokenHash);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversation");
            entity.HasKey(conversation => conversation.Id);
            entity.HasOne(conversation => conversation.Session)
                .WithMany(session => session.Conversations)
                .HasForeignKey(conversation => conversation.SessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("ChatMessage");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Role).IsRequired();
            entity.Property(message => message.Content).IsRequired();
            entity.HasOne(message => message.Conversation)
                .WithMany(conversation => conversation.ChatMessages)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
