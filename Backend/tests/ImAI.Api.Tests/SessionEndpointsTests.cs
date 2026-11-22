using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImAI.Api.Sessions;
using ImAI.Application.Sessions;
using ImAI.Application.Abstractions;
using ImAI.Application.Chat;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ImAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImAI.Api.Tests;

public sealed class SessionEndpointsTests
{
    [Fact]
    public async Task CreationHidesTokenInSecureHttpOnlyRootCookieAndStoresHash()
    {
        using var host = new SessionHost();
        using var client = host.CreateReadyClient();
        var response = await client.PostAsync("/api/sessions", null);
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl?.NoStore);
        var header = response.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("secure", header.ToLowerInvariant());
        Assert.Contains("httponly", header.ToLowerInvariant());
        Assert.Contains("path=/", header);
        Assert.Contains("samesite=lax", header.ToLowerInvariant());
        Assert.DoesNotContain("domain=", header.ToLowerInvariant());
        var token = ExtractToken(response);
        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(session);
        Assert.Equal(host.Clock.GetUtcNow().UtcDateTime.AddDays(7), session.ExpiresAt);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "sessionId", "expiresAt" }, json.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        using var scope = host.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().AnonymousSessions.SingleAsync();
        Assert.Equal(session.SessionId, row.Id);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))), row.TokenHash);
        Assert.NotEqual(token, row.TokenHash);
        SetCookie(client, token);
        var validation = await client.PostAsync("/api/sessions/validate", null);
        validation.EnsureSuccessStatusCode();
        Assert.Equal(session, await validation.Content.ReadFromJsonAsync<SessionResponse>());
    }

    [Fact]
    public async Task RefreshRotatesCookiePreservesSessionAndRenewsTtl()
    {
        using var host = new SessionHost();
        using var client = host.CreateReadyClient();
        var issued = await CreateSession(client);
        host.Clock.Advance(TimeSpan.FromDays(2));
        SetCookie(client, issued.Token);
        var response = await client.PostAsync("/api/sessions/refresh", null);
        response.EnsureSuccessStatusCode();
        var refreshed = await response.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(refreshed);
        Assert.Equal(issued.Session.SessionId, refreshed.SessionId);
        Assert.Equal(host.Clock.GetUtcNow().UtcDateTime.AddDays(7), refreshed.ExpiresAt);
        var newToken = ExtractToken(response);
        Assert.NotEqual(issued.Token, newToken);
        Assert.DoesNotContain(newToken, await response.Content.ReadAsStringAsync());
        foreach (var route in new[] { "validate", "refresh" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/sessions/" + route, null)).StatusCode);
        SetCookie(client, newToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/sessions/validate", null)).StatusCode);
    }

    [Theory]
    [InlineData("/api/sessions/validate")]
    [InlineData("/api/sessions/refresh")]
    [InlineData("/api/ai/chat")]
    [InlineData("/api/documents")]
    public async Task ProtectedRoutesRejectMissingAndExpiredCookies(string path)
    {
        using var host = new SessionHost();
        using var client = host.CreateReadyClient();
        var missing = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        var issued = await CreateSession(client);
        SetCookie(client, issued.Token);
        host.Clock.Advance(TimeSpan.FromDays(7));
        var expired = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", expired.Headers.GetValues("Set-Cookie").Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("malformed-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task InvalidCookiesAreRejected(string token)
    {
        using var host = new SessionHost();
        using var client = host.CreateReadyClient();
        SetCookie(client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/sessions/validate", null)).StatusCode);
    }

    [Fact]
    public async Task BodyTokenDoesNotAuthenticateAndCrossOriginRequestsAreRejected()
    {
        using var host = new SessionHost();
        using var client = host.CreateReadyClient();
        var issued = await CreateSession(client);
        var response = await client.PostAsJsonAsync("/api/sessions/validate", new { token = issued.Token });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        SetCookie(client, issued.Token);
        client.DefaultRequestHeaders.Add("Origin", "https://attacker.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/sessions/refresh", null)).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin");
        client.DefaultRequestHeaders.Add("Origin", "https://localhost:4200");
        response = await client.PostAsync("/api/sessions/validate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task ConcurrentRefreshOnlyIssuesOneReplacement()
    {
        using var host = new SessionHost();
        using var client = host.CreateReadyClient();
        var issued = await CreateSession(client);
        SetCookie(client, issued.Token);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsync("/api/sessions/refresh", null)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidCookieMakesCurrentSessionAndClaimsAvailableToChat()
    {
        using var host = new SessionHost();
        using var client = host.CreateReadyClient();
        var issued = await CreateSession(client);
        SetCookie(client, issued.Token);
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "hello" });
        response.EnsureSuccessStatusCode();
        var chat = await response.Content.ReadFromJsonAsync<AiChatResponse>();
        Assert.NotNull(chat);
        Assert.Equal(issued.Session.SessionId + ":" + issued.Session.SessionId, chat.Result);
    }

    private sealed class ContextChatService(IHttpContextAccessor accessor) : IAzureChatService
    {
        public Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
        {
            var context = accessor.HttpContext!;
            return Task.FromResult(context.GetCurrentSession()!.SessionId + ":" + context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        }
    }

    private static string ExtractToken(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single().Split(';')[0].Split('=', 2)[1];
    private static void SetCookie(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", SessionCookie.Name + "=" + token);
    }
    private static async Task<(SessionResponse Session, string Token)> CreateSession(HttpClient client)
    {
        var response = await client.PostAsync("/api/sessions", null);
        response.EnsureSuccessStatusCode();
        return ((await response.Content.ReadFromJsonAsync<SessionResponse>())!, ExtractToken(response));
    }

    private sealed class SessionHost : WebApplicationFactory<Program>
    {
        private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"imai-session-test-{Guid.NewGuid():N}.db");
        public TestClock Clock { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ImAI"] = $"Data Source={databasePath};Pooling=False"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAzureChatService>();
                services.AddScoped<IAzureChatService, ContextChatService>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
            });
        }
        public HttpClient CreateReadyClient()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ImAiDbContext>().Database.EnsureCreated();
            return client;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }
}
