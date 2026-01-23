namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for WSL (Windows Subsystem for Linux) operations.
/// </summary>
public interface IWslService
{
    /// <summary>
    /// Gets the default WSL distribution name.
    /// </summary>
    Task<string?> GetDefaultDistroAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if dotnet CLI is available in the specified WSL distro.
    /// </summary>
    Task<bool> CheckDotnetAvailableAsync(string? distro = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a command in the specified WSL distro.
    /// </summary>
    Task<ProcessResult> RunCommandAsync(string command, string? distro = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the dev certificate status in WSL.
    /// </summary>
    Task<(bool exists, bool trusted)> CheckCertStatusAsync(string? distro = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a certificate into WSL using dotnet dev-certs.
    /// </summary>
    Task<bool> ImportCertAsync(string windowsCertPath, string password, bool trust = false, string? distro = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans the dev certificate from WSL.
    /// </summary>
    Task<bool> CleanCertAsync(string? distro = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets certificate information in JSON format from WSL.
    /// </summary>
    Task<(string json, int exitCode)> GetCertificateInfoJsonAsync(string? distro = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a Windows path to a WSL path.
    /// </summary>
    string ConvertToWslPath(string windowsPath);
}
