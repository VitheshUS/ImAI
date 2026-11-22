using ImAI.Application.Sessions;

namespace ImAI.Application.Abstractions;

public interface IAnonymousSessionService
{
    Task<SessionResponse> CreateAsync(CancellationToken cancellationToken = default);
    Task<SessionResponse?> ValidateAsync(string? token, CancellationToken cancellationToken = default);
    Task<SessionResponse?> RefreshAsync(string? token, CancellationToken cancellationToken = default);
}
