namespace ImAI.Api.Logging;

public sealed class FileLoggingOptions
{
    public const string SectionName = "Logging:File";

    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "logs/app-.jsonl";
    public string AiChatPath { get; set; } = "logs/ai-chat-.jsonl";
    public long FileSizeLimitBytes { get; set; } = 10 * 1024 * 1024;
    public int RetainedFileCountLimit { get; set; } = 14;
}
