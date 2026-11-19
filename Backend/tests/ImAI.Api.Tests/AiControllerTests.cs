using System.Net;
using System.Net.Http.Json;
using ImAI.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ImAI.Api.Tests;

public sealed class AiControllerTests
{
    private static WebApplicationFactory<Program> CreateFactory(IAzureChatService service) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAzureChatService>();
            services.AddSingleton(service);
        }));

    [Fact]
    public async Task PostAcceptsLongQueryAndReturnsUnwrappedResponse()
    {
        var service = new FakeAzureChatService();
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient();
        var query = new string('a', 20000);
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { query });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Raw answer\nwith a second line.", await response.Content.ReadAsStringAsync());
        Assert.Equal(query, service.Question);
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
