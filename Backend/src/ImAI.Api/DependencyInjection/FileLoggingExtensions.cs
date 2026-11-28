using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;
using Serilog.Events;
using ImAI.Api.Logging;

namespace ImAI.Api.DependencyInjection;

public static class FileLoggingExtensions
{
    public static ILoggingBuilder AddFileLogging(this ILoggingBuilder logging, IConfiguration configuration, IHostEnvironment environment)
    {
        logging.Services.AddOptions<FileLoggingOptions>()
            .Bind(configuration.GetSection(FileLoggingOptions.SectionName))
            .Validate(options => !options.Enabled || (!string.IsNullOrWhiteSpace(options.Path)
                && !string.IsNullOrWhiteSpace(options.AiChatPath)
                && options.FileSizeLimitBytes > 0 && options.RetainedFileCountLimit > 0),
                "Logging:File requires application and AI chat paths and positive file size and retention limits.")
            .ValidateOnStart();
        // Keep the existing console providers and Microsoft Logging:LogLevel filtering.
        // The container owns and disposes this provider; no process-wide static logger.
        logging.Services.AddSingleton<ILoggerProvider>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<FileLoggingOptions>>().Value;
            var logger = new LoggerConfiguration().MinimumLevel.Verbose().Enrich.FromLogContext();
            if (options.Enabled)
            {
                var appPath = System.IO.Path.GetFullPath(options.Path, environment.ContentRootPath);
                var aiChatPath = System.IO.Path.GetFullPath(options.AiChatPath, environment.ContentRootPath);
                if (string.Equals(appPath, aiChatPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidOperationException("Application and AI chat logs must use different file paths.");
                logger.WriteTo.Logger(chat => WriteFile(chat.Filter.ByIncludingOnly(IsAiChatEvent), aiChatPath, options));
                logger.WriteTo.Logger(app => WriteFile(app.Filter.ByExcluding(IsAiChatEvent), appPath, options));
            }
            return new SerilogLoggerProvider(logger.CreateLogger(), dispose: true);
        });
        return logging;
    }

    private static bool IsAiChatEvent(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("SourceContext", out var source)
        && source is ScalarValue { Value: string category }
        && (category.StartsWith("ImAI.Application.Chat.", StringComparison.Ordinal)
            || category.StartsWith("ImAI.Infrastructure.Chat.", StringComparison.Ordinal));

    private static void WriteFile(LoggerConfiguration logger, string path, FileLoggingOptions options) =>
        logger.MinimumLevel.Verbose().WriteTo.File(new CompactJsonFormatter(), path,
            rollingInterval: RollingInterval.Day,
            fileSizeLimitBytes: options.FileSizeLimitBytes,
            rollOnFileSizeLimit: true,
            retainedFileCountLimit: options.RetainedFileCountLimit,
            shared: true);
}
