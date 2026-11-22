using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace ImAI.Api.Tests;

public sealed class SwaggerTests
{
    [Fact]
    public async Task DevelopmentServesSwaggerAndDocumentsAllApiRoutes()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        var ui = await client.GetAsync("/swagger/index.html");
        ui.EnsureSuccessStatusCode();
        Assert.Contains("swagger-ui", await ui.Content.ReadAsStringAsync());
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty("/api/ai/chat").TryGetProperty("post", out _));
        Assert.False(paths.TryGetProperty("/api/chat", out _));
        Assert.True(paths.GetProperty("/api/sessions").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/sessions/validate").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/sessions/refresh").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/documents").TryGetProperty("post", out _));
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task ProductionDoesNotServeApiDocumentation(string path)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }
}

