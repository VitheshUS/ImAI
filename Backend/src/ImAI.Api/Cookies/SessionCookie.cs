using ImAI.Application.Abstractions;

namespace ImAI.Api.Cookies;

public sealed class SessionCookie(IHttpContextAccessor accessor) : ISessionTokenWriter
{
    public const string Name = "__Host-ImAI.Session";
    public string? Read(HttpContext context) => context.Request.Cookies[Name];
    public void Write(string token, DateTime expiresAt) =>
        (accessor.HttpContext ?? throw new InvalidOperationException("Session cookie requires an active HTTP request."))
        .Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)),
            IsEssential = true
        });
}
