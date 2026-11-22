using ImAI.Api.Middleware;
using ImAI.Api.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImAI.Api.Tests;

public sealed class RequestPolicyTests
{
    [Fact]
    public async Task ClientCancellationDoesNotBecomeAnInternalServerError()
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None));
        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public void OriginChecksNormalizeCaseAndDefaultPorts()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://frontend.example:443/"
        }).Build();
        var policy = new BrowserOrigins(configuration);
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("api.example");
        Assert.True(policy.Allows("https://API.EXAMPLE:443", request));
        Assert.True(policy.Allows("https://FRONTEND.EXAMPLE", request));
        Assert.False(policy.Allows("https://frontend.example.attacker.test", request));
        Assert.False(policy.Allows("https://frontend.example/path", request));
        Assert.False(policy.Allows("null", request));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example")]
    [InlineData("https://example/path")]
    [InlineData("https://user:password@example")]
    public void InvalidCorsConfigurationIsRejected(string origin)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = origin
        }).Build();
        Assert.Throws<InvalidOperationException>(() => new BrowserOrigins(configuration));
    }
}
