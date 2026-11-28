using ImAI.Api.DependencyInjection;
using ImAI.Api.Middleware;
using ImAI.Api.Endpoints;
using ImAI.Application.DependencyInjection;
using ImAI.Infrastructure.DependencyInjection;
using ImAI.Infrastructure.Persistence;
using ImAI.SqliteExplorer;
using Microsoft.EntityFrameworkCore;
using ImAI.SqliteExplorer.DependencyInjection;
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFileLogging(builder.Configuration, builder.Environment);
builder.Services.AddApplication().AddInfrastructure(builder.Configuration).AddApi(builder.Configuration);
// Temporary local development tool; remove this block and MapSqliteExplorer to detach it.
if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SqliteExplorer:Enabled"))
    builder.Services.AddSqliteExplorer(provider => provider.GetRequiredService<ImAiDbContext>().Database.GetConnectionString()!);
var app = builder.Build();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
app.UseMiddleware<SessionOriginMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("../openapi/v1.json", "ImAI API v1");
        options.DocumentTitle = "ImAI API - Swagger";
    });
}
app.MapSessionEndpoints();
app.MapAiEndpoints();
app.MapConversationEndpoints();
app.MapDocumentEndpoints();
app.MapSqliteExplorer();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.Run();
public partial class Program;
