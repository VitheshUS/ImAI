using ImAI.Domain.Documents;
namespace ImAI.Application.Abstractions;

public interface IDocumentStorage
{
    Task<DocumentReceipt> ReceiveAsync(Stream content, string fileName, string contentType, long sizeBytes, CancellationToken cancellationToken);
}
