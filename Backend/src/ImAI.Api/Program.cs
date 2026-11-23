using ImAI.Api;
using ImAI.Api.Middleware;
using ImAI.Api.Endpoints;
using ImAI.Application;
using ImAI.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication().AddInfrastructure(builder.Configuration).AddApi(builder.Configuration);
var app = builder.Build();
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).WithTags("Health");
app.Run();
public partial class Program;
