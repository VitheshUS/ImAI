namespace ImAI.Domain.Documents;

public sealed record DocumentReceipt(Guid Id, string FileName, string ContentType, long SizeBytes, string Status, bool IsSimulated);
