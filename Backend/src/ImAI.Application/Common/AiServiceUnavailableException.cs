namespace ImAI.Application.Common;

public sealed class AiServiceUnavailableException(string message, Exception innerException) : Exception(message, innerException);
