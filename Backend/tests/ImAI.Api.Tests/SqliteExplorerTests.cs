using System.Net;
using System.Net.Http.Json;
using ImAI.Domain.Entities;
using ImAI.Infrastructure.Persistence;
using ImAI.SqliteExplorer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ImAI.Api.Tests;

public sealed class SqliteExplorerTests
{
    [Fact]
    public async Task LocalDevelopmentUiAndSchemaAreAvailable()
    {
        using var host = new ExplorerHost();
        using var client = await host.CreateDatabaseClient();
        var page = await client.GetAsync("/dev/database");
        page.EnsureSuccessStatusCode();
        Assert.Contains("Database explorer", await page.Content.ReadAsStringAsync());
        Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
        Assert.Contains("frame-ancestors 'none'", page.Headers.GetValues("Content-Security-Policy").Single());
        foreach (var asset in new[] { "explorer.js", "explorer.css" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/dev/database/" + asset)).StatusCode);
        var tables = (await client.GetFromJsonAsync<SqliteTable[]>("/dev/database/tables"))!;
        var messages = Assert.Single(tables, table => table.Name == "ChatMessage");
        Assert.Contains(messages.Columns, column => column.Name == "Sequence" && column.Type == "INTEGER");
        Assert.Contains(tables, table => table.Name == "ConversationSummary");
    }

    [Fact]
    public async Task BackendCodeCanQueryThroughTheInjectableService()
    {
        using var host = new ExplorerHost();
        using var client = await host.CreateDatabaseClient();
        using var scope = host.Services.CreateScope();
        var explorer = scope.ServiceProvider.GetRequiredService<ISqliteExplorer>();
        var result = await explorer.QueryAsync("SELECT Sequence, Content FROM ChatMessage ORDER BY Sequence");
        Assert.Equal(new[] { "Sequence", "Content" }, result.Columns);
        Assert.Equal(1L, Assert.Single(result.Rows)[0]);
        Assert.Equal("Saved question", result.Rows[0][1]);
    }

    [Theory]
    [InlineData("SELECT 42 AS Answer, NULL AS Value, ';' AS Separator; -- trailing comment")]
    [InlineData("/* read */ WITH items AS (SELECT 42 AS Answer) SELECT Answer, NULL AS Value, ';' AS Separator FROM items;")]
    public async Task ReadsSupportCommentsCtesAndQuotedSemicolons(string sql)
    {
        using var host = new ExplorerHost();
        using var client = await host.CreateDatabaseClient();
        var response = await client.PostAsJsonAsync("/dev/database/query", new { sql });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var rows = result.GetProperty("rows");
        Assert.Equal(42, rows[0][0].GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, rows[0][1].ValueKind);
        Assert.Equal(";", rows[0][2].GetString());
    }

    [Theory]
    [InlineData("DELETE FROM ChatMessage")]
    [InlineData("WITH items AS (SELECT 1) DELETE FROM ChatMessage")]
    [InlineData("SELECT 1; DELETE FROM ChatMessage")]
    [InlineData("SELECT 1; PRAGMA query_only = OFF")]
    [InlineData("CREATE TEMP TABLE test (Id INTEGER)")]
    [InlineData("ATTACH DATABASE ':memory:' AS other")]
    [InlineData("PRAGMA writable_schema = ON")]
    [InlineData("/* no query */")]
    [InlineData(null)]
    public async Task RejectsWritesAndMultipleStatementsWithoutChangingData(string? sql)
    {
        using var host = new ExplorerHost();
        using var client = await host.CreateDatabaseClient();
        var response = await client.PostAsJsonAsync("/dev/database/query", new { sql });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = host.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ImAiDbContext>().ChatMessages.CountAsync());
    }

    [Fact]
    public async Task LimitsResultsToFiveHundredRows()
    {
        using var host = new ExplorerHost();
        using var client = await host.CreateDatabaseClient();
        using var scope = host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISqliteExplorer>().QueryAsync(
            "WITH RECURSIVE numbers(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM numbers WHERE n < 600) SELECT n FROM numbers");
        Assert.Equal(500, result.Rows.Count);
        Assert.True(result.Truncated);
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task ComponentCanBeDisabledAndIsNeverMappedInProduction(string environment, bool enabled)
    {
        using var host = new ExplorerHost(environment, enabled);
        using var client = await host.CreateDatabaseClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dev/database")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/dev/database/tables")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/dev/database/query", new { sql = "SELECT 1" })).StatusCode);
        if (environment == "Production")
        {
            using var scope = host.Services.CreateScope();
            Assert.Null(scope.ServiceProvider.GetService<ISqliteExplorer>());
        }
    }

    [Fact]
    public async Task RejectsNonLocalHostsAndForeignOrigins()
    {
        using var host = new ExplorerHost();
        using var client = await host.CreateDatabaseClient();
        client.DefaultRequestHeaders.Host = "example.test";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/dev/database/tables")).StatusCode);
        client.DefaultRequestHeaders.Host = "localhost";
        client.DefaultRequestHeaders.Add("Origin", "https://localhost:4200");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/dev/database/tables")).StatusCode);
    }

    private sealed class ExplorerHost(string environment = "Development", bool enabled = true) : WebApplicationFactory<Program>
    {
        private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"imai-explorer-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ImAI"] = $"Data Source={databasePath};Pooling=False",
                ["SqliteExplorer:Enabled"] = enabled.ToString()
            }));
        }

        public async Task<HttpClient> CreateDatabaseClient()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            using var scope = Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<ImAiDbContext>();
            await database.Database.MigrateAsync();
            database.Conversations.Add(new Conversation
            {
                Id = Guid.NewGuid(),
                Session = new AnonymousSession { Id = Guid.NewGuid(), TokenHash = "test" },
                ChatMessages = [new ChatMessage { Id = Guid.NewGuid(), Sequence = 1, Role = "user", Content = "Saved question" }]
            });
            await database.SaveChangesAsync();
            return client;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && File.Exists(databasePath)) File.Delete(databasePath);
        }
    }
}
