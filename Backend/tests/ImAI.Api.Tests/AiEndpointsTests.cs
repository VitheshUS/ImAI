using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImAI.Api.Cookies;
using ImAI.Application.Abstractions;
using ImAI.Application.Abstractions.Messaging;
using ImAI.Application.Chat;
using ImAI.Application.Dtos;
using ImAI.Application.Dtos.Events;
using ImAI.Application.Common;
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
    [Theory]
    [InlineData(null, 6L, false)]
    [InlineData(null, 5L, true)]
    [InlineData(2L, 4L, false)]
    [InlineData(2L, 3L, true)]
    [InlineData(4L, 3L, false)]
    [InlineData(4L, 1L, true)]
    public async Task SummaryPublicationUsesStrictThresholdAndTheSummaryCheckpoint(long? boundary, long limit, bool shouldPublish)
    {
        using var factory = new ConversationHost { KLimit = limit };
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "first question");
        await Send(client, "second question", first.ConversationId);
        if (boundary is { } checkpoint)
        {
            using var seedScope = factory.Services.CreateScope();
            var database = seedScope.ServiceProvider.GetRequiredService<ImAiDbContext>();
            database.ConversationSummaries.Add(new ImAI.Domain.Entities.ConversationSummary
            {
                ConversationId = first.ConversationId, Content = "Saved summary", LastSummarizedSequence = checkpoint
            });
            await database.SaveChangesAsync();
        }
        factory.SummaryEvents.Clear();
        factory.SequencesAtPublication.Clear();

        await Send(client, "current question", first.ConversationId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        var conversation = await db.Conversations.SingleAsync();
        Assert.Equal(6, await db.ChatMessages.CountAsync());
        Assert.Equal(boundary.HasValue ? 1 : 0, await db.ConversationSummaries.CountAsync());
        Assert.Equal(boundary.HasValue ? "Saved summary" : string.Empty, factory.Calls[2].ConversationSummary);
        if (shouldPublish)
        {
            Assert.Equal(new ConversationSummaryRequestedEvent(first.ConversationId, conversation.SessionId, boundary ?? 0), Assert.Single(factory.SummaryEvents));
            Assert.Equal(6, Assert.Single(factory.SequencesAtPublication));
        }
        else
        {
            Assert.Empty(factory.SummaryEvents);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RejectedOrFailedPublicationDoesNotFailTheSavedChat(bool accepted, bool throws)
    {
        using var factory = new ConversationHost { KLimit = 1, AcceptSummary = accepted, ThrowOnPublish = throws };
        using var client = await factory.CreateSessionClient();

        var reply = await Send(client, "question");

        Assert.Equal(ConversationHost.Answer, reply.Result);
        Assert.Equal(0, Assert.Single(factory.SummaryEvents).LastSummarizedSequence);
        Assert.Equal(2, Assert.Single(factory.SequencesAtPublication));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().ChatMessages.CountAsync());
    }

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
        Assert.Equal(1, conversation.ChatMessages.Single(m => m.Role == "user").Sequence);
        Assert.Equal(2, conversation.ChatMessages.Single(m => m.Role == "assistant").Sequence);
        Assert.Equal(query, conversation.ChatMessages.Single(m => m.Role == "user").Content);
        Assert.Equal(reply.MessageId, conversation.ChatMessages.Single(m => m.Role == "assistant").Id);
        Assert.Equal(query, factory.Calls.Single().CurrentMessage);
        Assert.Equal("You are ImAI, an intelligent portfolio assistant.", factory.Calls.Single().SystemInstructions);
        Assert.Empty(factory.Calls.Single().RecentMessages);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "conversationId", "messageId", "result", "source" }, json.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task FollowUpWithoutSummarySendsStoredHistoryAndCurrentQueryToAi()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "hello");
        var second = await Send(client, "follow up", first.ConversationId);
        Assert.Equal(first.ConversationId, second.ConversationId);
        Assert.NotEqual(first.MessageId, second.MessageId);
        Assert.Equal(new[] { "user", "assistant" }, factory.Calls[1].RecentMessages.Select(message => message.Role));
        Assert.Equal(new[] { "hello", ConversationHost.Answer }, factory.Calls[1].RecentMessages.Select(message => message.Content));
        Assert.DoesNotContain(factory.Calls[1].RecentMessages, message => message.Content == "follow up");
        Assert.Empty(factory.Calls[1].ConversationSummary);
        Assert.Equal("follow up", factory.Calls[1].CurrentMessage);
        using var scope = factory.Services.CreateScope();
        var conversation = await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().Conversations.Include(c => c.ChatMessages).SingleAsync();
        Assert.Equal(4, conversation.ChatMessages.Count);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, conversation.ChatMessages.OrderBy(m => m.Sequence).Select(m => m.Sequence));
        Assert.True(conversation.UpdatedAt > conversation.CreatedAt);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().ConversationSummaries.CountAsync());
    }

    [Fact]
    public async Task FollowUpSendsSummaryAndOnlyUnsummarizedMessagesToAi()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "summarized question");
        await Send(client, "unsummarized question", first.ConversationId);
        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
            database.ConversationSummaries.Add(new ImAI.Domain.Entities.ConversationSummary
            {
                ConversationId = first.ConversationId,
                Content = "The user is learning about retrieval.",
                LastSummarizedSequence = 2
            });
            await database.SaveChangesAsync();
        }

        var reply = await Send(client, "current question", first.ConversationId);

        var context = factory.Calls[2];
        Assert.Equal(first.ConversationId, reply.ConversationId);
        Assert.Equal("The user is learning about retrieval.", context.ConversationSummary);
        Assert.Equal("You are ImAI, an intelligent portfolio assistant.", context.SystemInstructions);
        Assert.Equal(new[] { "user", "assistant" }, context.RecentMessages.Select(message => message.Role));
        Assert.Equal(new[] { "unsummarized question", ConversationHost.Answer }, context.RecentMessages.Select(message => message.Content));
        Assert.Equal("current question", context.CurrentMessage);
        Assert.Empty(context.RetrievedKnowledge);
        using var finalScope = factory.Services.CreateScope();
        Assert.Equal(6, await finalScope.ServiceProvider.GetRequiredService<ImAiDbContext>().ChatMessages.CountAsync());
    }

    [Fact]
    public async Task OneBrowserSessionCanCreateIndependentConversations()
    {
        using var factory = new ConversationHost();
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "first chat");
        var second = await Send(client, "new chat");
        Assert.NotEqual(first.ConversationId, second.ConversationId);
        Assert.Empty(factory.Calls[1].RecentMessages);
        var continued = await Send(client, "continue first chat", first.ConversationId);
        Assert.Equal(first.ConversationId, continued.ConversationId);
        Assert.Equal(new[] { "first chat", ConversationHost.Answer }, factory.Calls[2].RecentMessages.Select(message => message.Content));
        Assert.DoesNotContain(factory.Calls[2].RecentMessages, message => message.Content == "new chat");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(1, await db.AnonymousSessions.CountAsync());
        Assert.Equal(2, await db.Conversations.CountAsync());
        Assert.Single(await db.Conversations.Select(c => c.SessionId).Distinct().ToListAsync());
        Assert.All(await db.Conversations.Include(c => c.ChatMessages).ToListAsync(), conversation =>
            Assert.Equal(1, conversation.ChatMessages.Min(m => m.Sequence)));
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
        using var factory = new ConversationHost { KLimit = 1 };
        using var client = await factory.CreateSessionClient();
        var first = await Send(client, "hello");
        factory.SummaryEvents.Clear();
        factory.Fail = true;
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "fail", conversationId = first.ConversationId });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "fail new conversation" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(1, await db.Conversations.CountAsync());
        Assert.Equal(2, await db.ChatMessages.CountAsync());
        Assert.Empty(factory.SummaryEvents);
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
        using var factory = new ConversationHost { KLimit = 1 };
        using var client = await factory.CreateSessionClient();
        var original = await Send(client, "start");
        factory.SummaryEvents.Clear();
        factory.SequencesAtPublication.Clear();
        factory.SynchronizeCalls = true;
        var responses = await Task.WhenAll(new[] { "next one", "next two" }.Select(query =>
            client.PostAsJsonAsync("/api/ai/chat", new { query, conversationId = original.ConversationId })));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(1, await db.Conversations.CountAsync());
        Assert.Equal(4, await db.ChatMessages.CountAsync());
        Assert.Single(factory.SummaryEvents);
        Assert.Equal(4, Assert.Single(factory.SequencesAtPublication));
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

    [Fact]
    public async Task MissingAiCredentialsReturn503WithoutExposingCredentialDetails()
    {
        using var factory = new ConversationHost { FailAuthentication = true };
        using var client = await factory.CreateSessionClient();
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "hello" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("AI service authentication is not configured", body);
        Assert.DoesNotContain("private credential detail", body);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().Conversations.CountAsync());
    }

    private static async Task<AiChatResponse> Send(HttpClient client, string query, Guid? conversationId = null)
    {
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query, conversationId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiChatResponse>())!;
    }

    private sealed class ConversationHost : WebApplicationFactory<Program>, IAzureChatService, IConversationSummaryEventPublisher
    {
        public const string Answer = "AI answer";
        private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"imai-chat-test-{Guid.NewGuid():N}.db");
        public List<ConversationContextDto> Calls { get; } = [];
        public List<ConversationSummaryRequestedEvent> SummaryEvents { get; } = [];
        public List<long> SequencesAtPublication { get; } = [];
        public long KLimit { get; set; } = 10;
        public bool AcceptSummary { get; set; } = true;
        public bool ThrowOnPublish { get; set; }
        public bool Fail { get; set; }
        public bool FailAuthentication { get; set; }
        public bool SynchronizeCalls { get; set; }
        private readonly TaskCompletionSource bothCallsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ImAI"] = $"Data Source={databasePath};Pooling=False",
                ["ConversationSummary:KLimit"] = KLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAzureChatService>();
                services.AddSingleton<IAzureChatService>(this);
                services.AddSingleton<IConversationSummaryEventPublisher>(this);
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
        public async Task<string> AskAsync(ConversationContextDto context, CancellationToken cancellationToken = default)
        {
            lock (Calls)
            {
                Calls.Add(context);
                if (SynchronizeCalls && Calls.Count >= 3) bothCallsStarted.TrySetResult();
            }
            if (SynchronizeCalls) await bothCallsStarted.Task.WaitAsync(cancellationToken);
            if (FailAuthentication) throw new AiServiceUnavailableException("AI service authentication is not configured on the server.", new InvalidOperationException("private credential detail"));
            if (Fail) throw new InvalidOperationException("Test AI failure");
            return Answer;
        }
        public bool TryPublish(ConversationSummaryRequestedEvent summaryEvent)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
            var latestSequence = db.ChatMessages.Where(message => message.ConversationId == summaryEvent.ConversationId
                && message.Conversation.SessionId == summaryEvent.SessionId).Max(message => message.Sequence);
            lock (SummaryEvents)
            {
                SummaryEvents.Add(summaryEvent);
                SequencesAtPublication.Add(latestSequence);
            }
            if (ThrowOnPublish) throw new InvalidOperationException("Test publication failure");
            return AcceptSummary;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
