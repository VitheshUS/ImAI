using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ImAI.Application.Abstractions;
using ImAI.Application.Chat;
using ImAI.Infrastructure;

using var identityEvents = new IdentityEvents();
using var httpEvents = DiagnosticListener.AllListeners.Subscribe(new HttpEvents());
var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/ImAI.Api"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = contentRoot, EnvironmentName = "Development", ApplicationName = "ImAI.Api" });
var configuration = builder.Configuration;
Console.WriteLine("CONFIG PROVIDERS (last wins): " + string.Join(" -> ", ((IConfigurationRoot)configuration).Providers.Select(p => p.GetType().Name)));
foreach (var key in new[] { "AzureOpenAI:Endpoint", "AzureOpenAI:ChatDeployment", "AzureOpenAI:TenantId", "AzureOpenAI:Credential", "AzureOpenAI:AzureCliDirectory" })
{
    var configurationProvider = ((IConfigurationRoot)configuration).Providers.LastOrDefault(p => p.TryGet(key, out _));
    Console.WriteLine($"CONFIG {key}={configuration[key]} from {configurationProvider}");
}
Console.WriteLine("AZURE_TOKEN_CREDENTIALS=" + Environment.GetEnvironmentVariable("AZURE_TOKEN_CREDENTIALS"));
Console.WriteLine("AZURE_CONFIG_DIR=" + Environment.GetEnvironmentVariable("AZURE_CONFIG_DIR"));
var endpoint = new Uri(configuration["AzureOpenAI:Endpoint"]!);
var deployment = configuration["AzureOpenAI:ChatDeployment"]!;
var scope = "https://cognitiveservices.azure.com/.default";
var cliPath = configuration["AzureOpenAI:AzureCliDirectory"] ?? throw new InvalidOperationException("Configure AzureOpenAI:AzureCliDirectory for this diagnostic.");
Console.WriteLine("Inherited CLI PATH=" + (Environment.GetEnvironmentVariable("PATH") ?? "").Contains(cliPath, StringComparison.OrdinalIgnoreCase));
// Explicitly use the same portable CLI installation as the successful PowerShell call.
Environment.SetEnvironmentVariable("PATH", cliPath + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
var cli = new ObservedCredential(new AzureCliCredential(), "CLI");
AccessToken token;
try { token = await cli.GetTokenAsync(new TokenRequestContext([scope]), CancellationToken.None); }
catch (Exception exception) { Console.WriteLine("CLI_TOKEN_FAILED " + exception.GetType().Name); return; }
await Run("RAW_HTTP_CLI_TOKEN", async () =>
{
    using var http = new HttpClient();
    using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, $"openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version=2024-10-21"));
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    request.Content = new StringContent("{\"messages\":[{\"role\":\"user\",\"content\":\"Reply with OK.\"}]}", Encoding.UTF8, "application/json");
    using var response = await http.SendAsync(request);
    Console.WriteLine("RAW status=" + (int)response.StatusCode);
    if (!response.IsSuccessStatusCode) PrintError(await response.Content.ReadAsStringAsync());
});
var client = new AzureOpenAIClient(endpoint, cli, new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_10_21) { Audience = AzureOpenAIAudience.AzurePublicCloud }).GetChatClient(deployment);
await Run("SDK_WITHOUT_ADAPTER", async () => { var result = await client.CompleteChatAsync([new OpenAI.Chat.UserChatMessage("Reply with OK.")]); Console.WriteLine("SDK result received=" + (result.Value.Content.Count > 0)); });
await Run("SDK_BINARY_WITHOUT_ADAPTER", async () =>
{
    using var content = System.ClientModel.BinaryContent.Create(BinaryData.FromString("{\"messages\":[{\"role\":\"user\",\"content\":\"Reply with OK.\"}]}"));
    var result = await client.CompleteChatAsync(content);
    Console.WriteLine("SDK binary status=" + result.GetRawResponse().Status);
});
using var adapted = client.AsIChatClient();
await Run("SDK_WITH_ADAPTER", async () => { var result = await adapted.GetResponseAsync("Reply with OK."); Console.WriteLine("ADAPTER result length=" + result.Text.Length); });
var defaultCredential = new ObservedCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = true }), "DEFAULT");
await Run("DEFAULT_TOKEN", async () => { await defaultCredential.GetTokenAsync(new TokenRequestContext([scope]), CancellationToken.None); });
builder.Services.AddInfrastructure(configuration);
Console.WriteLine("IChatClient registrations=" + builder.Services.Count(s => s.ServiceType == typeof(IChatClient)));
using var provider = builder.Services.BuildServiceProvider();
Console.WriteLine("DI credential type=" + provider.GetRequiredService<TokenCredential>().GetType().Name);
await Run("EXISTING_DI_PIPELINE", async () =>
{
    using var serviceScope = provider.CreateScope();
    var result = await serviceScope.ServiceProvider.GetRequiredService<IAzureChatService>().AskAsync([new ChatTurn("user", "Reply with OK.")]);
    Console.WriteLine("DI result length=" + result.Length);
});

static async Task Run(string name, Func<Task> action)
{
    Console.WriteLine("LAYER " + name);
    try { await action(); Console.WriteLine("COMPLETE " + name); }
    catch (System.ClientModel.ClientResultException exception)
    {
        Console.WriteLine($"FAILED {name}: {exception.GetType().Name} status={exception.Status}");
        PrintError(exception.GetRawResponse()?.Content.ToString() ?? "");
    }
    catch (Exception exception) { Console.WriteLine($"FAILED {name}: {exception.GetType().Name} inner={exception.InnerException?.GetType().Name}"); if (exception is MissingMethodException) Console.WriteLine(exception.Message); }
}
static void PrintError(string content)
{
    try { using var json = JsonDocument.Parse(content); var error = json.RootElement.GetProperty("error"); Console.WriteLine("SERVICE_ERROR " + System.Text.RegularExpressions.Regex.Replace(error.GetRawText(), @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[redacted token]")); }
    catch { Console.WriteLine("Response body is not a JSON service error."); }
}

sealed class ObservedCredential(TokenCredential inner, string name) : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext context, CancellationToken cancellationToken) => GetTokenAsync(context, cancellationToken).AsTask().GetAwaiter().GetResult();
    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine(name + " requested scopes=" + string.Join(",", context.Scopes));
        var token = await inner.GetTokenAsync(context, cancellationToken);
        TokenInfo.Print(name, token.Token);
        return token;
    }
}
static class TokenInfo
{
    public static void Print(string label, string token)
    {
        try
        {
            var text = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            using var json = JsonDocument.Parse(Convert.FromBase64String(text.PadRight((text.Length + 3) / 4 * 4, '=')));
            var root = json.RootElement;
            string Get(string key) => root.TryGetProperty(key, out var value) ? value.ToString() : "(absent)";
            string Identity(string value) => value == "(absent)" ? value : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
            Console.WriteLine($"{label} token aud={Get("aud")} tid={Get("tid")} identity={Identity(Get("oid"))} exp={Get("exp")}");
        }
        catch { Console.WriteLine(label + " token claims could not be decoded (token omitted)."); }
    }
}
sealed class IdentityEvents : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource) { if (eventSource.Name == "Azure-Identity") EnableEvents(eventSource, EventLevel.Informational); }
    protected override void OnEventWritten(EventWrittenEventArgs args)
    {
        if (args.EventName?.Contains("CredentialSelected", StringComparison.OrdinalIgnoreCase) == true)
            Console.WriteLine("IDENTITY_SELECTED " + string.Join(" ", args.Payload ?? []));
    }
}
sealed class HttpEvents : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    public void OnNext(DiagnosticListener listener) { if (listener.Name == "HttpHandlerDiagnosticListener") listener.Subscribe(this); }
    public void OnNext(KeyValuePair<string, object?> item)
    {
        if (!item.Key.EndsWith(".Start", StringComparison.Ordinal) || item.Value is null) return;
        var request = item.Value.GetType().GetProperty("Request")?.GetValue(item.Value) as HttpRequestMessage;
        if (request?.RequestUri?.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase) != true) return;
        Console.WriteLine("HTTP " + request.Method + " " + request.RequestUri);
        if (request.Headers.Authorization is { Scheme: "Bearer", Parameter: { } bearer }) TokenInfo.Print("HTTP_AUTH", bearer);
        else Console.WriteLine("HTTP_AUTH no bearer authorization header");
    }
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}
