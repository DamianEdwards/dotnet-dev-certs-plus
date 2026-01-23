using System.Diagnostics;
using System.Text;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Utility for running external processes.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    /// <summary>
    /// Default singleton instance for use when DI is not available.
    /// </summary>
    public static ProcessRunner Default { get; } = new();

    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Read stdout/stderr concurrently with waiting for exit to prevent deadlocks
        // when output buffers fill up. The process may block if buffers are full,
        // so we must consume output while waiting.
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var exitTask = process.WaitForExitAsync(cancellationToken);

        // Wait for all three to complete - reads must happen concurrently with exit wait
        await Task.WhenAll(outputTask, errorTask, exitTask);

        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    /// <summary>
    /// Escapes a string for safe use as a Windows command-line argument.
    /// </summary>
    public static string EscapeArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "\"\"";
        }

        // If the argument contains spaces, quotes, or special characters, wrap in quotes and escape internal quotes
        if (argument.Contains(' ') || argument.Contains('"') || argument.Contains('\\'))
        {
            // Escape backslashes before quotes and escape quotes
            var escaped = argument
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        }

        return argument;
    }

    /// <summary>
    /// Escapes a string for safe use in a POSIX shell (bash/sh) single-quoted context.
    /// Single quotes are the safest way to quote in shell - only single quotes themselves need escaping.
    /// </summary>
    public static string EscapeShellArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            return "''";
        }

        // In single quotes, only single quotes need to be escaped.
        // We do this by ending the single quote, adding an escaped single quote, and starting a new single quote.
        // Example: "it's" becomes 'it'\''s'
        var escaped = argument.Replace("'", "'\\''");
        return $"'{escaped}'";
    }
}

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
    public string CombinedOutput => string.IsNullOrEmpty(StandardError)
        ? StandardOutput
        : $"{StandardOutput}\n{StandardError}";
}
