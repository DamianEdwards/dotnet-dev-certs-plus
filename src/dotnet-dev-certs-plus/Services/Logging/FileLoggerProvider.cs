using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotnetDevCertsPlus.Services.Logging;

/// <summary>
/// A logging provider that writes to date-based log files with automatic retention.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public FileLoggerProvider(FileLoggerOptions options)
    {
        _options = options;
        CleanupOldLogFiles();
    }

    public FileLoggerProvider(IOptions<FileLoggerOptions> options) : this(options.Value)
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _options));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _loggers.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Deletes log files older than the retention period.
    /// </summary>
    private void CleanupOldLogFiles()
    {
        try
        {
            if (!Directory.Exists(_options.LogDirectory))
            {
                return;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-_options.RetentionDays);
            var pattern = $"{_options.FilePrefix}-*.log";
            var logFiles = Directory.GetFiles(_options.LogDirectory, pattern);

            foreach (var filePath in logFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var datePart = fileName.Replace($"{_options.FilePrefix}-", "");
                    
                    if (DateTime.TryParse(datePart, out var fileDate) && fileDate < cutoffDate)
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                    // Ignore individual file deletion errors
                }
            }
        }
        catch
        {
            // Ignore cleanup errors - should not interrupt normal operation
        }
    }
}

/// <summary>
/// Extension methods for adding file logging to ILoggingBuilder.
/// </summary>
public static class FileLoggerExtensions
{
    /// <summary>
    /// Adds a file logger to the logging builder.
    /// </summary>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder)
    {
        return builder.AddFile(new FileLoggerOptions());
    }

    /// <summary>
    /// Adds a file logger to the logging builder with the specified options.
    /// </summary>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, FileLoggerOptions options)
    {
        builder.AddProvider(new FileLoggerProvider(options));
        return builder;
    }

    /// <summary>
    /// Adds a file logger to the logging builder with configuration.
    /// </summary>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, Action<FileLoggerOptions> configure)
    {
        var options = new FileLoggerOptions();
        configure(options);
        return builder.AddFile(options);
    }
}
