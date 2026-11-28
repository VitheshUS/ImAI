using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ImAI.SqliteExplorer;

public static class SqliteExplorerExtensions
{
    public static WebApplication MapSqliteExplorer(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("SqliteExplorer:Enabled")) return app;

        var group = app.MapGroup("/dev/database").ExcludeFromDescription().AllowAnonymous();
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            var host = http.Request.Host.Host;
            var localHost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(host, out var hostAddress) && IPAddress.IsLoopback(hostAddress));
            if (!localHost || (http.Connection.RemoteIpAddress is { } remote && !IPAddress.IsLoopback(remote)))
                return Results.Problem(statusCode: 403, title: "Database explorer is available only on localhost.");
            var origin = http.Request.Headers.Origin.ToString();
            if ((!string.IsNullOrEmpty(origin) && !string.Equals(origin, $"{http.Request.Scheme}://{http.Request.Host}", StringComparison.OrdinalIgnoreCase))
                || http.Request.Headers["Sec-Fetch-Site"] == "cross-site")
                return Results.Problem(statusCode: 403, title: "Use the database explorer from the backend origin.");

            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            try { return await next(context); }
            catch (ArgumentException exception) { return Results.Problem(statusCode: 400, title: "Query rejected", detail: exception.Message); }
            catch (SqliteException exception) { return Results.Problem(statusCode: 400, title: "SQLite query failed", detail: exception.Message); }
        });
        group.MapGet("/", () => Asset("index.html", "text/html; charset=utf-8"));
        group.MapGet("/explorer.js", () => Asset("explorer.js", "text/javascript; charset=utf-8"));
        group.MapGet("/explorer.css", () => Asset("explorer.css", "text/css; charset=utf-8"));
        group.MapGet("/tables", async (ISqliteExplorer explorer, CancellationToken cancellationToken) =>
            Results.Ok(await explorer.GetTablesAsync(cancellationToken)));
        group.MapPost("/query", async (SqlQuery request, ISqliteExplorer explorer, CancellationToken cancellationToken) =>
            Results.Ok(await explorer.QueryAsync(request.Sql, cancellationToken)));
        return app;
    }

    private static IResult Asset(string name, string contentType) => Results.Stream(
        typeof(SqliteExplorerExtensions).Assembly.GetManifestResourceStream($"ImAI.SqliteExplorer.Assets.{name}")
            ?? throw new InvalidOperationException("Database explorer asset is missing."), contentType);

    private sealed record SqlQuery(string? Sql);
}
