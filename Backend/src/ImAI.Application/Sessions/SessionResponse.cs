namespace ImAI.Application.Sessions;

public sealed record SessionResponse(Guid SessionId, DateTime ExpiresAt);
