using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Service for WSL (Windows Subsystem for Linux) operations.
/// </summary>
public partial class WslService
{
    /// <summary>
    /// Gets the default WSL distribution name.
    /// </summary>
    public async Task<string?> GetDefaultDistroAsync(CancellationToken cancellationToken = default)
    {
        EnsureWindows();

        var result = await ProcessRunner.RunAsync("wsl.exe", "--list --quiet", cancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return null;
        }

        // First line is the default distro
        var lines = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return lines.Length > 0 ? CleanDistroName(lines[0]) : null;
    }

    /// <summary>
    /// Checks if dotnet CLI is available in the specified WSL distro.
    /// </summary>
    public async Task<bool> CheckDotnetAvailableAsync(string? distro = null, CancellationToken cancellationToken = default)
    {
        EnsureWindows();

        var args = BuildWslArgs(distro, "which dotnet");
        var result = await ProcessRunner.RunAsync("wsl.exe", args, cancellationToken);

        return result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    /// <summary>
    /// Runs a command in the specified WSL distro.
    /// </summary>
    public async Task<ProcessResult> RunCommandAsync(
        string command,
        string? distro = null,
        CancellationToken cancellationToken = default)
    {
        EnsureWindows();

        var args = BuildWslArgs(distro, command);
        return await ProcessRunner.RunAsync("wsl.exe", args, cancellationToken);
    }

    /// <summary>
    /// Checks the dev certificate status in WSL.
    /// </summary>
    public async Task<(bool exists, bool trusted)> CheckCertStatusAsync(
        string? distro = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("dotnet dev-certs https --check --trust", distro, cancellationToken);

        // Exit code 0 = exists and trusted
        // Exit code non-zero with specific output = exists but not trusted or doesn't exist
        var exists = result.ExitCode == 0 ||
                     !result.CombinedOutput.Contains("No valid certificate", StringComparison.OrdinalIgnoreCase);
        var trusted = result.ExitCode == 0;

        return (exists, trusted);
    }

    /// <summary>
    /// Imports a certificate into WSL using dotnet dev-certs.
    /// </summary>
    public async Task<bool> ImportCertAsync(
        string windowsCertPath,
        string password,
        bool trust = false,
        string? distro = null,
        CancellationToken cancellationToken = default)
    {
        // Convert Windows path to WSL path (e.g., C:\temp\cert.pfx -> /mnt/c/temp/cert.pfx)
        var wslPath = ConvertToWslPath(windowsCertPath);

        var command = $"dotnet dev-certs https --import \"{wslPath}\" --password \"{password}\"";
        if (trust)
        {
            command += " --trust";
        }

        var result = await RunCommandAsync(command, distro, cancellationToken);
        return result.Success;
    }

    /// <summary>
    /// Cleans the dev certificate from WSL using dotnet dev-certs https --clean.
    /// </summary>
    public async Task<bool> CleanCertAsync(string? distro = null, CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("dotnet dev-certs https --clean", distro, cancellationToken);
        return result.Success;
    }

    /// <summary>
    /// Converts a Windows path to a WSL path.
    /// </summary>
    public static string ConvertToWslPath(string windowsPath)
    {
        // C:\Users\foo\file.txt -> /mnt/c/Users/foo/file.txt
        var match = DriveLetterRegex().Match(windowsPath);
        if (match.Success)
        {
            var driveLetter = match.Groups[1].Value.ToLowerInvariant();
            var remainingPath = match.Groups[2].Value.Replace('\\', '/');
            return $"/mnt/{driveLetter}{remainingPath}";
        }

        // Fallback: just replace backslashes
        return windowsPath.Replace('\\', '/');
    }

    private static string BuildWslArgs(string? distro, string command)
    {
        if (string.IsNullOrEmpty(distro))
        {
            return $"-- {command}";
        }

        return $"-d {distro} -- {command}";
    }

    private static string CleanDistroName(string name)
    {
        // WSL output may contain null characters or whitespace
        return name.Trim().Replace("\0", "").Trim();
    }

    private static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("WSL operations are only supported on Windows.");
        }
    }

    [GeneratedRegex(@"^([A-Za-z]):(.*)$")]
    private static partial Regex DriveLetterRegex();
}
