using DotnetDevCertsPlus.Models;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for dev certificate operations via dotnet dev-certs CLI.
/// </summary>
public interface IDevCertService
{
    /// <summary>
    /// Checks the current status of the dev certificate.
    /// </summary>
    Task<DevCertStatus> CheckStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the dev certificate exists, creating it if necessary.
    /// </summary>
    Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports the dev certificate to a file.
    /// </summary>
    Task<bool> ExportAsync(string path, CertificateFormat format, string? password = null, CancellationToken cancellationToken = default);
}
