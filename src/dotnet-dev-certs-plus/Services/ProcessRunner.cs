using System.Diagnostics;

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

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        return new ProcessResult(process.ExitCode, output, error);
    }

    /// <summary>
    /// Escapes a string for safe use as a command-line argument.
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
}

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
    public string CombinedOutput => string.IsNullOrEmpty(StandardError)
        ? StandardOutput
        : $"{StandardOutput}\n{StandardError}";
}
