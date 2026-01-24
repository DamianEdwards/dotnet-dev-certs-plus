namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for logging update check diagnostics.
/// </summary>
public interface IUpdateLogger
{
    /// <summary>
    /// Logs a message to the update check log file.
    /// </summary>
    void Log(string message);

    /// <summary>
    /// Logs a message with a category prefix.
    /// </summary>
    void Log(string category, string message);

    /// <summary>
    /// Logs multiple lines with a category prefix.
    /// </summary>
    void Log(string category, IEnumerable<string> lines);
}

/// <summary>
/// Logs update check diagnostics to ~/.dotnet-dev-certs-plus/update-check.log
/// </summary>
public class UpdateLogger : IUpdateLogger
{
    private const string StateDirectoryName = ".dotnet-dev-certs-plus";
    private const string LogFileName = "update-check.log";
    private const int MaxLogEntries = 500;
    private const int TrimToEntries = 100;
    private const int TrimCheckInterval = 50; // Only check trim every N writes

    private readonly string _logFilePath;
    private readonly object _lock = new();
    private int _writeCount;

    /// <summary>
    /// Creates a new UpdateLogger with the default log file path.
    /// </summary>
    public UpdateLogger() : this(GetDefaultLogFilePath())
    {
    }

    /// <summary>
    /// Creates a new UpdateLogger with a custom log file path (for testing).
    /// </summary>
    public UpdateLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    /// <inheritdoc/>
    public void Log(string message)
    {
        WriteLogEntry(message);
    }

    /// <inheritdoc/>
    public void Log(string category, string message)
    {
        WriteLogEntry($"[{category}] {message}");
    }

    /// <inheritdoc/>
    public void Log(string category, IEnumerable<string> lines)
    {
        var linesList = lines.ToList();
        if (linesList.Count == 0)
        {
            return;
        }

        var entries = new List<string> { $"[{category}]" };
        entries.AddRange(linesList.Select(l => $"  {l}"));
        WriteLogEntries(entries);
    }

    private void WriteLogEntry(string message)
    {
        WriteLogEntries([message]);
    }

    private void WriteLogEntries(IEnumerable<string> messages)
    {
        try
        {
            lock (_lock)
            {
                EnsureDirectoryExists();
                
                // Only check if trim is needed periodically to avoid reading file on every write
                _writeCount++;
                if (_writeCount >= TrimCheckInterval)
                {
                    _writeCount = 0;
                    TrimLogIfNeeded();
                }

                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                var lines = messages.Select((m, i) => i == 0 ? $"[{timestamp}] {m}" : $"  {m}");
                File.AppendAllLines(_logFilePath, lines);
            }
        }
        catch
        {
            // Ignore logging errors - diagnostics should never interrupt normal operation
        }
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void TrimLogIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                return;
            }

            var lines = File.ReadAllLines(_logFilePath);
            if (lines.Length > MaxLogEntries)
            {
                // Keep only the last TrimToEntries lines
                var trimmedLines = lines.Skip(lines.Length - TrimToEntries).ToArray();
                File.WriteAllLines(_logFilePath, trimmedLines);
            }
        }
        catch
        {
            // Ignore trim errors
        }
    }

    private static string GetDefaultLogFilePath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, StateDirectoryName, LogFileName);
    }
}

/// <summary>
/// A no-op implementation of IUpdateLogger for when logging is disabled.
/// </summary>
public class NullUpdateLogger : IUpdateLogger
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NullUpdateLogger Instance = new();

    private NullUpdateLogger() { }

    /// <inheritdoc/>
    public void Log(string message) { }

    /// <inheritdoc/>
    public void Log(string category, string message) { }

    /// <inheritdoc/>
    public void Log(string category, IEnumerable<string> lines) { }
}
