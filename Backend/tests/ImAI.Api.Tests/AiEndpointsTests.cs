using System.Net;
using System.Net.Http.Json;
using ImAI.Application.Abstractions;
using ImAI.Application.Chat;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImAI.Api.Tests;

public sealed class AiEndpointsTests
{
    private static WebApplicationFactory<Program> CreateFactory(IAzureChatService service) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAzureChatService>();
            services.AddSingleton(service);
        }));

    [Fact]
    public async Task PostAcceptsLongQueryAndReturnsStructuredResponse()
    {
        var service = new FakeAzureChatService();
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient();
        var query = new string('a', 20000);
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var reply = await response.Content.ReadFromJsonAsync<AiChatResponse>();
        Assert.NotNull(reply);
        Assert.NotEqual(Guid.Empty, reply.ConversationId);
        Assert.NotEqual(Guid.Empty, reply.MessageId);
        Assert.Equal("Raw answer\nwith a second line.", reply.Result);
        Assert.Empty(reply.Source);
        Assert.Equal(query, service.Question);
    }

    [Fact]
    public async Task RemovedDummyChatRouteReturns404()
    {
        await using var factory = CreateFactory(new FakeAzureChatService());
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/chat", new { query = "hello" })).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task InvalidQueryReturns400WithoutCallingService(string? query)
    {
        var service = new FakeAzureChatService();
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.Question);
    }

    [Fact]
    public async Task RepeatedRequestsPreserveConversationAndCreateUniqueMessages()
    {
        await using var factory = CreateFactory(new FakeAzureChatService());
        using var client = factory.CreateClient();
        var conversationId = Guid.NewGuid();
        var first = await client.PostAsJsonAsync("/api/ai/chat", new { query = "hello", conversationId });
        var second = await client.PostAsJsonAsync("/api/ai/chat", new { query = "follow up", conversationId });
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var a = await first.Content.ReadFromJsonAsync<AiChatResponse>();
        var b = await second.Content.ReadFromJsonAsync<AiChatResponse>();
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(conversationId, a.ConversationId);
        Assert.Equal(conversationId, b.ConversationId);
        Assert.NotEqual(a.MessageId, b.MessageId);
        using var json = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "conversationId", "messageId", "result", "source" },
            json.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("source").ValueKind);
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task InvalidConversationIdReturns400(string conversationId)
    {
        var service = new FakeAzureChatService();
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query = "hello", conversationId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.Question);
    }

    private sealed class FakeAzureChatService : IAzureChatService
    {
        public string? Question { get; private set; }
        public Task<string> AskAsync(string question, CancellationToken cancellationToken = default)
        {
            Question = question;
            return Task.FromResult("Raw answer\nwith a second line.");
        }
    }
}
