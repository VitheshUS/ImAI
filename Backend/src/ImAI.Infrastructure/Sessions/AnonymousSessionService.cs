using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using ImAI.Application.Abstractions;
using ImAI.Application.Sessions;
using ImAI.Domain.Entities;
using ImAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImAI.Infrastructure.Sessions;

internal sealed class AnonymousSessionService(ImAiDbContext database, TimeProvider clock, ISessionTokenWriter tokenWriter) : IAnonymousSessionService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    public async Task<SessionResponse> CreateAsync(CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();
        var now = clock.GetUtcNow().UtcDateTime;
        var session = new AnonymousSession
        {
            Id = Guid.NewGuid(),
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.Add(TokenLifetime)
        };
        database.AnonymousSessions.Add(session);
        await database.SaveChangesAsync(cancellationToken);
        tokenWriter.Write(token, session.ExpiresAt);
        return new(session.Id, session.ExpiresAt);
    }

    public async Task<SessionResponse?> ValidateAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (!IsTokenFormatValid(token)) return null;
        var tokenHash = Hash(token);
        var now = clock.GetUtcNow().UtcDateTime;
        return await database.AnonymousSessions.AsNoTracking()
            .Where(session => session.TokenHash == tokenHash && session.ExpiresAt > now)
            .Select(session => new SessionResponse(session.Id, session.ExpiresAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<SessionResponse?> RefreshAsync(string? token, CancellationToken cancellationToken = default)
    {
        var session = await ValidateAsync(token, cancellationToken);
        if (session is null || token is null) return null;

        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.Add(TokenLifetime);
        var oldHash = Hash(token);
        var newToken = GenerateToken();
        var newHash = Hash(newToken);
        // Compare-and-swap: only one refresh can replace a given token.
        var updated = await database.AnonymousSessions
            .Where(value => value.Id == session.SessionId && value.TokenHash == oldHash && value.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(value => value.TokenHash, newHash)
                .SetProperty(value => value.ExpiresAt, expiresAt), cancellationToken);

        if (updated != 1) return null;
        tokenWriter.Write(newToken, expiresAt);
        return new(session.SessionId, expiresAt);
    }

    private static string GenerateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool IsTokenFormatValid([NotNullWhen(true)] string? token) =>
        token is { Length: 64 } && token.All(character => char.IsAsciiHexDigit(character));
}
