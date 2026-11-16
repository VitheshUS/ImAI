using ImAI.Application.Abstractions;
using ImAI.Domain.Documents;
namespace ImAI.Infrastructure.Storage;
// Replace with an Azure Blob implementation in the storage integration phase.
internal sealed class StubDocumentStorage : IDocumentStorage
{
    public async Task<DocumentReceipt> ReceiveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken)
    {
        // Consume the incoming stream, without persisting bytes or contacting Azure.
        await content.CopyToAsync(Stream.Null, cancellationToken);
        return new(Guid.NewGuid(), fileName, contentType, sizeBytes, "received-not-stored", true);
    }
}
