using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImAI.Api.Cookies;
using ImAI.Application.Abstractions;
using ImAI.Application.Chat;
using ImAI.Application.Sessions;
using ImAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImAI.Api.Tests;

public sealed class AiEndpointsTests
{
    [Fact]
    public async Task FirstPromptCreatesConversationAndSavesBothMessages()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var query = new string('a', 20000);
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query });
        response.EnsureSuccessStatusCode();
        var reply = (await response.Content.ReadFromJsonAsync<AiChatResponse>())!;
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ConversationHost.Answer, reply.Result);
        Assert.Empty(reply.Source);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        var conversation = await db.Conversations.Include(c => c.ChatMessages).SingleAsync();
        Assert.Equal(reply.ConversationId, conversation.Id);
        Assert.Equal((await db.AnonymousSessions.SingleAsync()).Id, conversation.SessionId);
        Assert.Equal(2, conversation.ChatMessages.Count);
        Assert.Equal(query, conversation.ChatMessages.Single(m => m.Role == "user").Content);
        Assert.Equal(reply.MessageId, conversation.ChatMessages.Single(m => m.Role == "assistant").Id);
        Assert.Equal(query, factory.Calls.Single().Single().Content);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "conversationId", "messageId", "result", "source" }, json.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task FollowUpKeepsStoredConversationButSendsOnlyCurrentQueryToAi()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "hello");
        var second = await Send(client, "follow up", first.ConversationId);
        Assert.Equal(first.ConversationId, second.ConversationId);
        Assert.NotEqual(first.MessageId, second.MessageId);
        Assert.Equal(new[] { "user" }, factory.Calls[1].Select(m => m.Role));
        Assert.Equal(new[] { "follow up" }, factory.Calls[1].Select(m => m.Content));
        using var scope = factory.Services.CreateScope();
        var conversation = await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().Conversations.Include(c => c.ChatMessages).SingleAsync();
        Assert.Equal(4, conversation.ChatMessages.Count);
        Assert.True(conversation.UpdatedAt > conversation.CreatedAt);
    }

    [Fact]
    public async Task OneBrowserSessionCanCreateIndependentConversations()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "first chat");
        var second = await Send(client, "new chat");
        Assert.NotEqual(first.ConversationId, second.ConversationId);
        Assert.Single(factory.Calls[1]);
        var continued = await Send(client, "continue first chat", first.ConversationId);
        Assert.Equal(first.ConversationId, continued.ConversationId);
        Assert.DoesNotContain(factory.Calls[2], message => message.Content == "new chat");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(1, await db.AnonymousSessions.CountAsync());
        Assert.Equal(2, await db.Conversations.CountAsync());
        Assert.Single(await db.Conversations.Select(c => c.SessionId).Distinct().ToListAsync());
    }

    [Fact]
    public async Task SessionCreationReusesValidBrowserCookie()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var first = await client.PostAsync("/api/sessions/validate", null);
        var original = await first.Content.ReadFromJsonAsync<SessionResponse>();
        var again = await client.PostAsync("/api/sessions", null);
        again.EnsureSuccessStatusCode();
        Assert.Equal(original, await again.Content.ReadFromJsonAsync<SessionResponse>());
        Assert.False(again.Headers.Contains("Set-Cookie"));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().AnonymousSessions.CountAsync());
    }

    [Fact]
    public async Task ForeignAndUnknownConversationIdsAreRejectedBeforeAiIsCalled()
    {
        using var factory = new ConversationHost();
        using var owner = await factory.CreateSessionClient();
        using var other = await factory.CreateSessionClient();
        var first = await Send(owner, "private");
        foreach (var id in new[] { first.ConversationId, Guid.NewGuid() })
        {
            var response = await other.PostAsJsonAsync("/api/ai/chat", new { query = "steal", conversationId = id });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.Single(factory.Calls);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().ChatMessages.CountAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task InvalidQueryDoesNotCallAiOrCreateConversation(string? query)
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai/chat", new { query })).StatusCode);
        Assert.Empty(factory.Calls);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().Conversations.CountAsync());
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidConversationIdReturns400(string conversationId)
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/ai/chat", new { query = "hello", conversationId })).StatusCode);
        Assert.Empty(factory.Calls);
    }

    [Fact]
    public async Task FailedAiCallDoesNotPersistPartialExchange()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "hello");
        factory.Fail = true;
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "fail", conversationId = first.ConversationId });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "fail new conversation" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(1, await db.Conversations.CountAsync());
        Assert.Equal(2, await db.ChatMessages.CountAsync());
    }

    [Fact]
    public async Task RemovedDummyRouteStaysUnavailable()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/chat", new { query = "hello" })).StatusCode);
    }

    [Fact]
    public async Task ConcurrentPromptsDoNotSilentlyOverwriteConversationHistory()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var original = await Send(client, "start");
        factory.SynchronizeCalls = true;
        var responses = await Task.WhenAll(new[] { "next one", "next two" }.Select(query =>
            client.PostAsJsonAsync("/api/ai/chat", new { query, conversationId = original.ConversationId })));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(1, await db.Conversations.CountAsync());
        Assert.Equal(4, await db.ChatMessages.CountAsync());
    }

    [Fact]
    public async Task SavedConversationsCanBeListedLoadedAndDeletedOnlyByTheirSession()
    {
        using var factory = new ConversationHost();
        using var owner = await factory.CreateSessionClient();
        using var other = await factory.CreateSessionClient();
        var first = await Send(owner, "saved chat");
        var list = await owner.GetFromJsonAsync<ConversationOverview[]>("/api/conversations");
        Assert.Single(list!);
        Assert.Equal(first.ConversationId, list![0].Id);
        Assert.Empty((await other.GetFromJsonAsync<ConversationOverview[]>("/api/conversations"))!);
        var details = await owner.GetFromJsonAsync<ConversationDetails>("/api/conversations/" + first.ConversationId);
        Assert.NotNull(details);
        Assert.Equal(new[] { "user", "assistant" }, details.Messages.Select(m => m.Role));
        Assert.Equal(first.MessageId, details.Messages[1].Id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/api/conversations/" + first.ConversationId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync("/api/conversations/" + first.ConversationId)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
            db.ConversationSummaries.Add(new ImAI.Domain.Entities.ConversationSummary { ConversationId = first.ConversationId });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync("/api/conversations/" + first.ConversationId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/conversations/" + first.ConversationId)).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var database = finalScope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(0, await database.ChatMessages.CountAsync());
        Assert.Equal(0, await database.ConversationSummaries.CountAsync());
        Assert.Equal(2, await database.AnonymousSessions.CountAsync());
    }

    private static async Task<AiChatResponse> Send(HttpClient client, string query, Guid? conversationId = null)
    {
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query, conversationId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiChatResponse>())!;
    }

    private sealed class ConversationHost : WebApplicationFactory<Program>, IAzureChatService
    {
        public const string Answer = "AI answer";
        private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"imai-chat-test-{Guid.NewGuid():N}.db");
        public List<ChatTurn[]> Calls { get; } = [];
        public bool Fail { get; set; }
        public bool SynchronizeCalls { get; set; }
        private readonly TaskCompletionSource bothCallsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:ImAI"] = $"Data Source={databasePath};Pooling=False" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAzureChatService>();
                services.AddSingleton<IAzureChatService>(this);
            });
        }
        public async Task<HttpClient> CreateSessionClient()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().Database.MigrateAsync();
            var session = await client.PostAsync("/api/sessions", null);
            session.EnsureSuccessStatusCode();
            client.DefaultRequestHeaders.Add("Cookie", session.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
            return client;
        }
        public async Task<string> AskAsync(IReadOnlyList<ChatTurn> messages, CancellationToken cancellationToken = default)
        {
            lock (Calls)
            {
                Calls.Add(messages.ToArray());
                if (SynchronizeCalls && Calls.Count >= 3) bothCallsStarted.TrySetResult();
            }
            if (SynchronizeCalls) await bothCallsStarted.Task.WaitAsync(cancellationToken);
            if (Fail) throw new InvalidOperationException("Test AI failure");
            return Answer;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
