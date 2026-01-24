using Microsoft.Extensions.Logging;

namespace DotnetDevCertsPlus.Services.Logging;

/// <summary>
/// Options for the file logger.
/// </summary>
public class FileLoggerOptions
{
    /// <summary>
    /// The directory where log files are stored.
    /// Defaults to ~/.dotnet-dev-certs-plus
    /// </summary>
    public string LogDirectory { get; set; } = GetDefaultLogDirectory();

    /// <summary>
    /// The prefix for log file names.
    /// Files will be named {FilePrefix}-{date}.log
    /// </summary>
    public string FilePrefix { get; set; } = "update-check";

    /// <summary>
    /// Number of days to retain log files.
    /// Files older than this will be automatically deleted.
    /// </summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// Minimum log level to write to the file.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

    private static string GetDefaultLogDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".dotnet-dev-certs-plus");
    }
}
