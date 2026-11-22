using ImAI.Application.Documents;
using ImAI.Domain.Documents;
using MediatR;
namespace ImAI.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/documents", async (IFormFile file, ISender sender, CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            return Results.Ok(await sender.Send(new ReceiveDocument(stream, file.FileName, file.ContentType, file.Length), cancellationToken));
        })
        // Origin validation middleware protects multipart cookie requests from cross-origin submissions.
        .DisableAntiforgery().RequireAuthorization().Produces(401).WithName("ReceiveDocument").WithTags("Documents")
        .Accepts<IFormFile>("multipart/form-data").Produces<DocumentReceipt>()
        .ProducesProblem(400).ProducesProblem(413).ProducesProblem(500);
        return endpoints;
    }
}
