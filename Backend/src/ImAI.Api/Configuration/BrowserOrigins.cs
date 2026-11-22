namespace ImAI.Api.Configuration;

public sealed class BrowserOrigins
{
    public string[] Allowed { get; }
    public BrowserOrigins(IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200", "https://localhost:4200"];
        Allowed = origins.Select(origin => Normalize(origin)
                ?? throw new InvalidOperationException("Cors:AllowedOrigins must contain HTTP(S) origins without paths, credentials, or wildcards."))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool Allows(string origin, HttpRequest request)
    {
        var normalized = Normalize(origin);
        return normalized is not null && (string.Equals(normalized,
            Normalize($"{request.Scheme}://{request.Host}"), StringComparison.OrdinalIgnoreCase)
            || Allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase));
    }

    private static string? Normalize(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && !uri.Host.Contains('*')
            ? uri.GetLeftPart(UriPartial.Authority) : null;
}
