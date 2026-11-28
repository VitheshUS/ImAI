using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImAI.Application.Chat;
using ImAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ImAI.Infrastructure.Chat;
using ImAI.Application.Dtos.Events;
using System.Threading.Channels;

namespace ImAI.Api.Tests;

public sealed class FileLoggingTests
{
    [Fact]
    public async Task LogsActualContextAiMessagesResponseAndSavedExchangeWithCorrelation()
    {
        await using var host = new LoggingHost();
        using var client = await host.CreateSessionClient();
        var first = await Send(client, "Summarized question");
        await Send(client, "Recent question", first.ConversationId);
        using (var scope = host.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
            database.ConversationSummaries.Add(new ImAI.Domain.Entities.ConversationSummary
            {
                ConversationId = first.ConversationId,
                Content = "Stored summary",
                LastSummarizedSequence = 2
            });
            await database.SaveChangesAsync();
        }
        var query = "Current question\n{\"message\":\"this is chat content\"}" + new string('q', 20000);
        var reply = await Send(client, query, first.ConversationId);
        var events = host.ReadEvents();
        var received = events.Last(entry => EventName(entry) == "ChatRequestReceived");
        var context = events.Last(entry => EventName(entry) == "ChatContextConstructed");
        var request = events.Last(entry => EventName(entry) == "AiRequestSending");
        var response = events.Last(entry => EventName(entry) == "AiResponseReceived");
        var saved = events.Last(entry => EventName(entry) == "ChatExchangeSaved");

        Assert.Equal(query, received.GetProperty("Query").GetString());
        var built = context.GetProperty("Context");
        Assert.Equal("Stored summary", built.GetProperty("ConversationSummary").GetString());
        Assert.Equal(query, built.GetProperty("CurrentMessage").GetString());
        Assert.Equal(2, built.GetProperty("RecentMessages").GetArrayLength());
        var messages = request.GetProperty("AiMessages");
        Assert.Equal(5, messages.GetArrayLength());
        Assert.Equal("system", messages[0].GetProperty("Role").GetString());
        Assert.Equal("You are ImAI, an intelligent portfolio assistant.", messages[0].GetProperty("Content").GetString());
        Assert.Contains("Stored summary", messages[1].GetProperty("Content").GetString());
        Assert.Equal("Recent question", messages[2].GetProperty("Content").GetString());
        Assert.Equal("assistant", messages[3].GetProperty("Role").GetString());
        Assert.Equal(query, messages[4].GetProperty("Content").GetString());
        Assert.Equal(LoggingHost.Answer, response.GetProperty("ResponseContent").GetString());
        Assert.Equal("test-response", response.GetProperty("AiResponseId").GetString());
        Assert.Equal(reply.MessageId, saved.GetProperty("MessageId").GetGuid());
        foreach (var entry in new[] { context, request, response, saved })
        {
            Assert.Equal(reply.ConversationId, entry.GetProperty("ConversationId").GetGuid());
            Assert.Equal(received.GetProperty("ChatOperationId").GetString(), entry.GetProperty("ChatOperationId").GetString());
            Assert.Equal(received.GetProperty("TraceId").GetString(), entry.GetProperty("TraceId").GetString());
        }
        var completed = events.Last(entry => EventName(entry) == "HttpRequestCompleted");
        Assert.Equal(200, completed.GetProperty("StatusCode").GetInt32());
        Assert.Equal(received.GetProperty("TraceId").GetString(), completed.GetProperty("TraceId").GetString());
        Assert.All(events, entry => Assert.DoesNotContain(host.SessionToken, entry.GetRawText()));

        var aiEvents = host.ReadEvents("ai-chat-*.jsonl");
        var applicationEvents = host.ReadEvents("app-*.jsonl");
        Assert.NotEmpty(aiEvents);
        Assert.NotEmpty(applicationEvents);
        foreach (var name in new[] { "ChatRequestReceived", "ConversationResolved", "ChatContextConstructed", "AiRequestSending", "AiResponseReceived", "ChatExchangeSaved" })
        {
            Assert.Contains(aiEvents, entry => EventName(entry) == name);
            Assert.DoesNotContain(applicationEvents, entry => EventName(entry) == name);
        }
        Assert.DoesNotContain(applicationEvents, entry => entry.TryGetProperty("AiMessages", out _) || entry.TryGetProperty("ResponseContent", out _)
            || entry.TryGetProperty("Context", out _) || entry.TryGetProperty("Query", out _));
        Assert.Contains(applicationEvents, entry => EventName(entry) == "HttpRequestCompleted");
        Assert.DoesNotContain(aiEvents, entry => EventName(entry) is "HttpRequestStarted" or "HttpRequestCompleted");
        Assert.Contains(applicationEvents, entry => entry.GetProperty("SourceContext").GetString() == "Microsoft.EntityFrameworkCore.Database.Command");
        Assert.DoesNotContain(aiEvents, entry => entry.GetProperty("SourceContext").GetString()!.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal));
        Assert.Equal(events.Length, aiEvents.Length + applicationEvents.Length);
    }

    [Fact]
    public async Task FollowUpWithoutSummaryIncludesHistoryInActualAiMessagesExactlyOnce()
    {
        await using var host = new LoggingHost();
        using var client = await host.CreateSessionClient();
        var first = await Send(client, "Previous question");
        await Send(client, "Current question", first.ConversationId);

        var events = host.ReadEvents("ai-chat-*.jsonl");
        var context = events.Last(entry => EventName(entry) == "ChatContextConstructed").GetProperty("Context");
        Assert.Empty(context.GetProperty("ConversationSummary").GetString()!);
        Assert.Equal(2, context.GetProperty("RecentMessages").GetArrayLength());
        var messages = events.Last(entry => EventName(entry) == "AiRequestSending").GetProperty("AiMessages").EnumerateArray().ToArray();
        Assert.Equal(new[] { "system", "user", "assistant", "user" }, messages.Select(message => message.GetProperty("Role").GetString()));
        Assert.Equal("Previous question", messages[1].GetProperty("Content").GetString());
        Assert.Equal(LoggingHost.Answer, messages[2].GetProperty("Content").GetString());
        Assert.Equal("Current question", messages[3].GetProperty("Content").GetString());
        Assert.Single(messages, message => message.GetProperty("Content").GetString() == "Current question");
        using var scope = host.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(4, await database.ChatMessages.CountAsync());
        Assert.Equal(0, await database.ConversationSummaries.CountAsync());
    }

    [Fact]
    public async Task SummaryRequestIsEnqueuedAndLoggedAfterTheChatIsSaved()
    {
        await using var host = new LoggingHost(kLimit: 1);
        using var client = await host.CreateSessionClient();

        var response = await Send(client, "Question");

        Assert.Equal(LoggingHost.Answer, response.Result);
        var events = host.ReadEvents("ai-chat-*.jsonl");
        var published = Assert.Single(events, entry => EventName(entry) == "ConversationSummaryPublishAttempt");
        Assert.True(published.GetProperty("Accepted").GetBoolean());
        Assert.Equal(0, published.GetProperty("LastSummarizedSequence").GetInt64());
        Assert.DoesNotContain(events, entry => EventName(entry) == "ConversationSummaryPublishFailed");
        Assert.DoesNotContain(events, entry => EventName(entry) == "AiRequestFailed");
        using var scope = host.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
        Assert.Equal(2, await database.ChatMessages.CountAsync());
        Assert.Equal(0, await database.ConversationSummaries.CountAsync());
        var reader = host.Services.GetRequiredService<ChannelReader<ConversationSummaryRequestedEvent>>();
        Assert.True(reader.TryRead(out var summaryEvent));
        Assert.NotNull(summaryEvent);
        Assert.Equal(response.ConversationId, summaryEvent.ConversationId);
        Assert.Equal((await database.Conversations.SingleAsync()).SessionId, summaryEvent.SessionId);
        Assert.Equal(0, summaryEvent.LastSummarizedSequence);
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public async Task FullSummaryQueueDropsTheNewEventAndKeepsChatSuccessful()
    {
        await using var host = new LoggingHost(kLimit: 1, queueCapacity: 1);
        using var client = await host.CreateSessionClient();
        var first = await Send(client, "First question");

        var second = await Send(client, "Second question").WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(LoggingHost.Answer, second.Result);
        var attempts = host.ReadEvents("ai-chat-*.jsonl").Where(entry => EventName(entry) == "ConversationSummaryPublishAttempt").ToArray();
        Assert.Equal(new[] { true, false }, attempts.Select(entry => entry.GetProperty("Accepted").GetBoolean()));
        var reader = host.Services.GetRequiredService<ChannelReader<ConversationSummaryRequestedEvent>>();
        Assert.True(reader.TryRead(out var summaryEvent));
        Assert.NotNull(summaryEvent);
        Assert.Equal(first.ConversationId, summaryEvent.ConversationId);
        Assert.False(reader.TryRead(out _));
        using var scope = host.Services.CreateScope();
        Assert.Equal(4, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().ChatMessages.CountAsync());
    }

    [Fact]
    public async Task LogsAiFailureAndFinalHttpStatusWithoutSavingAnExchange()
    {
        await using var host = new LoggingHost(failAi: true);
        using var client = await host.CreateSessionClient();
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "Question" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var events = host.ReadEvents();
        var failed = Assert.Single(events, entry => EventName(entry) == "AiRequestFailed");
        Assert.Contains("Test AI failure", failed.GetProperty("@x").GetString());
        Assert.Contains(host.ReadEvents("ai-chat-*.jsonl"), entry => EventName(entry) == "AiRequestFailed");
        Assert.DoesNotContain(host.ReadEvents("app-*.jsonl"), entry => EventName(entry) == "AiRequestFailed");
        Assert.DoesNotContain(events, entry => EventName(entry) == "ChatExchangeSaved");
        Assert.Equal(500, events.Last(entry => EventName(entry) == "HttpRequestCompleted").GetProperty("StatusCode").GetInt32());
        using var scope = host.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().ChatMessages.CountAsync());
    }

    [Fact]
    public async Task SplitFilesPreserveConfiguredDebugLogging()
    {
        await using var host = new LoggingHost(logLevel: "Debug");
        using var client = await host.CreateSessionClient();
        host.Services.GetRequiredService<ILogger<FileLoggingTests>>()
            .LogDebug(new EventId(4000, "ApplicationDebugMarker"), "Application debug message");
        host.Services.GetRequiredService<ILogger<AzureChatService>>()
            .LogDebug(new EventId(4001, "AiDebugMarker"), "AI debug message");
        Assert.Contains(host.ReadEvents("app-*.jsonl"), entry => EventName(entry) == "ApplicationDebugMarker");
        Assert.Contains(host.ReadEvents("ai-chat-*.jsonl"), entry => EventName(entry) == "AiDebugMarker");
    }

    [Fact]
    public async Task FileLoggingCanBeDisabledThroughConfiguration()
    {
        await using var host = new LoggingHost(enabled: false);
        using var client = await host.CreateSessionClient();
        await Send(client, "Question");
        Assert.Empty(host.ReadEvents());
    }

    private static string? EventName(JsonElement entry) => entry.TryGetProperty("EventId", out var eventId)
        && eventId.TryGetProperty("Name", out var name) ? name.GetString() : null;

    private static async Task<AiChatResponse> Send(HttpClient client, string query, Guid? conversationId = null)
    {
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query, conversationId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AiChatResponse>())!;
    }

    private sealed class LoggingHost(bool failAi = false, bool enabled = true, string logLevel = "Information", long kLimit = 10, int queueCapacity = 100) : WebApplicationFactory<Program>
    {
        public const string Answer = "AI answer\nwith \"quotes\".";
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"imai-logging-test-{Guid.NewGuid():N}");
        public string SessionToken { get; private set; } = string.Empty;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(directory);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ImAI"] = $"Data Source={Path.Combine(directory, "test.db")};Pooling=False",
                ["Logging:File:Enabled"] = enabled.ToString(),
                ["ConversationSummary:KLimit"] = kLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["ConversationSummary:Queue:Capacity"] = queueCapacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Logging:LogLevel:Default"] = logLevel,
                ["Logging:File:Path"] = Path.Combine(directory, "app-.jsonl"),
                ["Logging:File:AiChatPath"] = Path.Combine(directory, "ai-chat-.jsonl")
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(new TestChatClient(failAi));
            });
        }

        public async Task<HttpClient> CreateSessionClient()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().Database.MigrateAsync();
            var response = await client.PostAsync("/api/sessions", null);
            response.EnsureSuccessStatusCode();
            var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
            SessionToken = cookie[(cookie.IndexOf('=') + 1)..];
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            return client;
        }

        public JsonElement[] ReadEvents(string pattern = "*.jsonl") => Directory.EnumerateFiles(directory, pattern)
            .SelectMany(path =>
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                var events = new List<JsonElement>();
                while (reader.ReadLine() is { } line)
                {
                    using var document = JsonDocument.Parse(line);
                    events.Add(document.RootElement.Clone());
                }
                return events;
            }).ToArray();

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    private sealed class TestChatClient(bool fail) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            fail ? throw new InvalidOperationException("Test AI failure")
                : Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, LoggingHost.Answer)) { ResponseId = "test-response", ModelId = "test-model" });

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
