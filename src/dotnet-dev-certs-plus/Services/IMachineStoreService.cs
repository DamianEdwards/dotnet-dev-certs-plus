using DotnetDevCertsPlus.Models;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for machine certificate store operations.
/// </summary>
public interface IMachineStoreService
{
    /// <summary>
    /// Checks if the dev certificate exists in the machine store.
    /// </summary>
    Task<bool> CheckMachineStoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the dev certificate is trusted in the machine trust store.
    /// </summary>
    Task<bool> CheckMachineTrustAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports the dev certificate to the machine store.
    /// </summary>
    Task<bool> ImportToMachineStoreAsync(string pfxPath, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Trusts the dev certificate in the machine trust store.
    /// </summary>
    Task<bool> TrustInMachineStoreAsync(string pfxPath, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the dev certificate from the machine store and trust store.
    /// </summary>
    Task<bool> CleanMachineStoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets certificate information from the machine store for JSON output.
    /// </summary>
    Task<CertificateInfo?> GetCertificateInfoAsync(CancellationToken cancellationToken = default);
}
