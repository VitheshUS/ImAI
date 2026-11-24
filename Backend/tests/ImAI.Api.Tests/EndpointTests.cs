using System.Net;
using System.Net.Http.Json;
using System.Text;
using ImAI.Application.Abstractions;
using ImAI.Application.Chat;
using ImAI.Domain.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImAI.Api.Tests;

public sealed class EndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient client;
    private readonly WebApplicationFactory<Program> testFactory;
    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        testFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddTestAuthentication()));
        client = testFactory.CreateClient();
    }
    public void Dispose() { client.Dispose(); testFactory.Dispose(); }

    [Fact]
    public async Task MalformedJsonReturnsProblemDetails()
    {
        var response = await client.PostAsync("/api/ai/chat", new StringContent("{broken", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("notes.txt", "hello", HttpStatusCode.OK)]
    [InlineData("program.exe", "hello", HttpStatusCode.BadRequest)]
    [InlineData("empty.pdf", "", HttpStatusCode.BadRequest)]
    public async Task DocumentUploadValidatesAndDoesNotClaimPersistence(string name, string content, HttpStatusCode expected)
    {
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", name);
        var response = await client.PostAsync("/api/documents", multipart);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var receipt = await response.Content.ReadFromJsonAsync<DocumentReceipt>();
            Assert.NotNull(receipt);
            Assert.True(receipt.IsSimulated);
            Assert.Equal("received-not-stored", receipt.Status);
            Assert.Equal(content.Length, receipt.SizeBytes);
        }
    }

    [Fact]
    public async Task MissingFileReturnsBadRequest()
    {
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent("missing"), "other");
        var response = await client.PostAsync("/api/documents", multipart);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedExceptionDoesNotExposeDetails()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").ConfigureServices(services =>
            {
                services.AddTestAuthentication();
                services.RemoveAll<IAzureChatService>();
                services.AddScoped<IAzureChatService, ThrowingResponder>();
            }));
        using var failingClient = factory.CreateClient();
        var response = await failingClient.PostAsJsonAsync("/api/ai/chat", new { query = "hello" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-internal-detail", body);
        Assert.Contains("traceId", body);
    }
    private sealed class ThrowingResponder : IAzureChatService
    {
        public Task<string> AskAsync(ImAI.Application.Dtos.ConversationContextDto context, CancellationToken cancellationToken = default) => throw new InvalidOperationException("secret-internal-detail");
    }
}
