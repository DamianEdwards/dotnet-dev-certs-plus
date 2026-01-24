using Microsoft.Extensions.Logging;

namespace DotnetDevCertsPlus.Services.Logging;

/// <summary>
/// A logger that writes to date-based log files.
/// </summary>
public sealed class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileLoggerOptions _options;
    private readonly object _lock = new();

    public FileLogger(string categoryName, FileLoggerOptions options)
    {
        _categoryName = categoryName;
        _options = options;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _options.MinimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var logLine = FormatLogLine(logLevel, message, exception);
        WriteToFile(logLine);
    }

    private string FormatLogLine(LogLevel logLevel, string message, Exception? exception)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var level = GetLogLevelString(logLevel);
        var category = GetShortCategoryName(_categoryName);
        
        var line = $"[{timestamp}] [{level}] [{category}] {message}";
        
        if (exception is not null)
        {
            line += Environment.NewLine + $"  Exception: {exception.GetType().Name}: {exception.Message}";
            if (exception.StackTrace is not null)
            {
                line += Environment.NewLine + "  " + exception.StackTrace.Replace(Environment.NewLine, Environment.NewLine + "  ");
            }
        }

        return line;
    }

    private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };

    private static string GetShortCategoryName(string categoryName)
    {
        var lastDot = categoryName.LastIndexOf('.');
        return lastDot >= 0 ? categoryName[(lastDot + 1)..] : categoryName;
    }

    private void WriteToFile(string logLine)
    {
        try
        {
            lock (_lock)
            {
                EnsureDirectoryExists();
                var filePath = GetCurrentLogFilePath();
                File.AppendAllLines(filePath, [logLine]);
            }
        }
        catch
        {
            // Ignore logging errors - diagnostics should never interrupt normal operation
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_options.LogDirectory))
        {
            Directory.CreateDirectory(_options.LogDirectory);
        }
    }

    private string GetCurrentLogFilePath()
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var fileName = $"{_options.FilePrefix}-{date}.log";
        return Path.Combine(_options.LogDirectory, fileName);
    }
}
