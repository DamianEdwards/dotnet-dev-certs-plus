using DotnetDevCertsPlus.Models;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Wrapper for dotnet dev-certs https CLI commands.
/// </summary>
public class DevCertService
{
    /// <summary>
    /// Checks the current status of the dev certificate.
    /// </summary>
    public async Task<DevCertStatus> CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync("dotnet", "dev-certs https --check --trust", cancellationToken);
        return DevCertStatus.Parse(result.CombinedOutput, result.ExitCode);
    }

    /// <summary>
    /// Ensures the dev certificate exists, creating it if necessary.
    /// </summary>
    public async Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        var status = await CheckStatusAsync(cancellationToken);
        if (status.Exists)
        {
            return true;
        }

        var result = await ProcessRunner.RunAsync("dotnet", "dev-certs https", cancellationToken);
        return result.Success;
    }

    /// <summary>
    /// Exports the dev certificate to a file.
    /// </summary>
    /// <param name="path">The path to export the certificate to.</param>
    /// <param name="format">The format to export (Pfx or Pem).</param>
    /// <param name="password">The password for PFX export (required for Pfx format).</param>
    public async Task<bool> ExportAsync(
        string path,
        CertificateFormat format,
        string? password = null,
        CancellationToken cancellationToken = default)
    {
        var args = $"dev-certs https --export-path \"{path}\" --format {format}";

        if (format == CertificateFormat.Pfx && !string.IsNullOrEmpty(password))
        {
            args += $" --password \"{password}\"";
        }

        var result = await ProcessRunner.RunAsync("dotnet", args, cancellationToken);
        return result.Success;
    }
}

public enum CertificateFormat
{
    Pfx,
    Pem
}
