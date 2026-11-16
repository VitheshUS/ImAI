using ImAI.Application.Abstractions;
using ImAI.Application.Common;
using ImAI.Domain.Documents;
using MediatR;
namespace ImAI.Application.Documents;

public sealed record ReceiveDocument(Stream Content, string FileName, string ContentType, long SizeBytes) : IRequest<DocumentReceipt>;
public sealed class ReceiveDocumentHandler(IDocumentStorage storage) : IRequestHandler<ReceiveDocument, DocumentReceipt>
{
    public const long MaxFileSize = 50 * 1024 * 1024;
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".md", ".markdown", ".txt", ".csv", ".json" };
    public Task<DocumentReceipt> Handle(ReceiveDocument request, CancellationToken cancellationToken)
    {
        if (request.SizeBytes <= 0 || request.SizeBytes > MaxFileSize)
            throw new RequestValidationException("Document must be non-empty and no larger than 50 MB.");
        var fileName = Path.GetFileName(request.FileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName) || !Extensions.Contains(Path.GetExtension(fileName)))
            throw new RequestValidationException("Supported files: PDF, DOCX, Markdown, TXT, CSV, JSON.");
        return storage.ReceiveAsync(request.Content, fileName, request.ContentType, request.SizeBytes, cancellationToken);
    }
}
