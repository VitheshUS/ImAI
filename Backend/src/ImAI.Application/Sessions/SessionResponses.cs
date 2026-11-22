namespace ImAI.Application.Sessions;

public sealed record SessionValidationResponse(Guid SessionId, bool IsValid, DateTime ExpiresAt);

public sealed record SessionResponse(Guid SessionId, DateTime ExpiresAt);
