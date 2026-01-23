using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using DotnetDevCertsPlus.Models;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Service for machine certificate store operations across Windows, Linux, and macOS.
/// </summary>
public class MachineStoreService
{
    private const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
    private const string CertFileName = "aspnetcore-dev-cert.crt";

    // Linux paths
    private const string LinuxCertDir = "/usr/local/share/ca-certificates";
    private const string LinuxCertPath = $"{LinuxCertDir}/{CertFileName}";
    private const string LinuxTrustedCertPath = $"/etc/ssl/certs/{CertFileName}";

    /// <summary>
    /// Checks if the dev certificate exists in the machine store.
    /// </summary>
    public async Task<bool> CheckMachineStoreAsync(CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CheckWindowsMachineStore();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return File.Exists(LinuxCertPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await CheckMacOSSystemKeychainAsync(cancellationToken);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    /// <summary>
    /// Checks if the dev certificate is trusted in the machine trust store.
    /// </summary>
    public async Task<bool> CheckMachineTrustAsync(CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CheckWindowsMachineTrust();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux, if the cert is in ca-certificates and update-ca-certificates was run, it's trusted
            return File.Exists(LinuxCertPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await CheckMacOSTrustSettingsAsync(cancellationToken);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    /// <summary>
    /// Imports the dev certificate to the machine store.
    /// </summary>
    public async Task<bool> ImportToMachineStoreAsync(string pfxPath, string password, CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ImportToWindowsMachineStore(pfxPath, password);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await ImportToLinuxMachineStoreAsync(pfxPath, password, cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await ImportToMacOSSystemKeychainAsync(pfxPath, password, cancellationToken);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    /// <summary>
    /// Trusts the dev certificate in the machine trust store.
    /// </summary>
    public async Task<bool> TrustInMachineStoreAsync(string pfxPath, string password, CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TrustInWindowsMachineStore(pfxPath, password);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux, importing to ca-certificates and running update-ca-certificates is trusting
            return await ImportToLinuxMachineStoreAsync(pfxPath, password, cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await TrustInMacOSSystemKeychainAsync(pfxPath, password, cancellationToken);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    /// <summary>
    /// Removes the dev certificate from the machine store and trust store.
    /// </summary>
    public async Task<bool> CleanMachineStoreAsync(CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CleanWindowsMachineStore();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await CleanLinuxMachineStoreAsync(cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await CleanMacOSSystemKeychainAsync(cancellationToken);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    /// <summary>
    /// Gets certificate information from the machine store for JSON output.
    /// </summary>
    public async Task<CertificateInfo?> GetCertificateInfoAsync(CancellationToken cancellationToken = default)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsCertificateInfo();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await GetLinuxCertificateInfoAsync(cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await GetMacOSCertificateInfoAsync(cancellationToken);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    #region Windows Implementation

    private bool CheckWindowsMachineStore()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        return FindDevCert(store) is not null;
    }

    private bool CheckWindowsMachineTrust()
    {
        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        return FindDevCert(store) is not null;
    }

    private bool ImportToWindowsMachineStore(string pfxPath, string password)
    {
        try
        {
            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                pfxPath,
                password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            var existing = FindDevCert(store);
            if (existing is not null)
            {
                store.Remove(existing);
            }

            store.Add(cert);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TrustInWindowsMachineStore(string pfxPath, string password)
    {
        try
        {
            var certWithKey = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password);
            var publicCertBytes = certWithKey.Export(X509ContentType.Cert);
            var publicCert = X509CertificateLoader.LoadCertificate(publicCertBytes);

            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            var existing = FindDevCert(store);
            if (existing is not null)
            {
                store.Remove(existing);
            }

            store.Add(publicCert);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static X509Certificate2? FindDevCert(X509Store store)
    {
        foreach (var cert in store.Certificates)
        {
            foreach (var extension in cert.Extensions)
            {
                if (extension.Oid?.Value == AspNetHttpsOid)
                {
                    return cert;
                }
            }
        }
        return null;
    }

    private bool CleanWindowsMachineStore()
    {
        var myStoreSuccess = true;
        var rootStoreSuccess = true;

        // Remove from LocalMachine\My
        try
        {
            using var myStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            myStore.Open(OpenFlags.ReadWrite);
            var cert = FindDevCert(myStore);
            if (cert is not null)
            {
                myStore.Remove(cert);
            }
        }
        catch
        {
            myStoreSuccess = false;
        }

        // Remove from LocalMachine\Root
        try
        {
            using var rootStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            rootStore.Open(OpenFlags.ReadWrite);
            var cert = FindDevCert(rootStore);
            if (cert is not null)
            {
                rootStore.Remove(cert);
            }
        }
        catch
        {
            rootStoreSuccess = false;
        }

        // Return true if at least one store was cleaned successfully
        return myStoreSuccess || rootStoreSuccess;
    }

    private CertificateInfo? GetWindowsCertificateInfo()
    {
        using var myStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        myStore.Open(OpenFlags.ReadOnly);
        var cert = FindDevCert(myStore);

        if (cert is null)
        {
            return null;
        }

        // Check if trusted (in Root store)
        using var rootStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        rootStore.Open(OpenFlags.ReadOnly);
        var isTrusted = FindDevCert(rootStore) is not null;

        return CertificateInfo.FromCertificate(cert, isTrusted);
    }

    #endregion

    #region Linux Implementation

    private async Task<bool> ImportToLinuxMachineStoreAsync(string pfxPath, string password, CancellationToken cancellationToken)
    {
        try
        {
            // Load the PFX and export as PEM (public cert only)
            var cert = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password);
            var pemContent = ExportToPem(cert);

            // Write to temp file first
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, pemContent, cancellationToken);

            // Move to ca-certificates directory (requires sudo)
            var moveResult = await ProcessRunner.RunAsync("sudo", $"cp \"{tempFile}\" \"{LinuxCertPath}\"", cancellationToken);
            File.Delete(tempFile);

            if (!moveResult.Success)
            {
                return false;
            }

            // Update CA certificates
            var updateResult = await ProcessRunner.RunAsync("sudo", "update-ca-certificates", cancellationToken);
            return updateResult.Success;
        }
        catch
        {
            return false;
        }
    }

    private static string ExportToPem(X509Certificate2 cert)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("-----BEGIN CERTIFICATE-----");
        builder.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END CERTIFICATE-----");
        return builder.ToString();
    }

    private async Task<bool> CleanLinuxMachineStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(LinuxCertPath))
            {
                return true; // Already clean
            }

            // Remove the certificate file
            var removeResult = await ProcessRunner.RunAsync("sudo", $"rm -f \"{LinuxCertPath}\"", cancellationToken);
            if (!removeResult.Success)
            {
                return false;
            }

            // Update CA certificates
            var updateResult = await ProcessRunner.RunAsync("sudo", "update-ca-certificates", cancellationToken);
            return updateResult.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<CertificateInfo?> GetLinuxCertificateInfoAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(LinuxCertPath))
        {
            return null;
        }

        try
        {
            // Read the PEM certificate
            var pemContent = await File.ReadAllTextAsync(LinuxCertPath, cancellationToken);
            var cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(
                pemContent
                    .Replace("-----BEGIN CERTIFICATE-----", "")
                    .Replace("-----END CERTIFICATE-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim()));

            // Check if the certificate is actually trusted by verifying it exists in /etc/ssl/certs/
            // (created by update-ca-certificates)
            var isTrusted = File.Exists(LinuxTrustedCertPath) ||
                            File.Exists($"/etc/ssl/certs/{cert.Thumbprint}.0"); // Alternative symlink format

            return CertificateInfo.FromCertificate(cert, isTrusted);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region macOS Implementation

    private async Task<bool> CheckMacOSSystemKeychainAsync(CancellationToken cancellationToken)
    {
        // Search for ASP.NET dev cert in System keychain
        var result = await ProcessRunner.RunAsync(
            "security",
            "find-certificate -c \"localhost\" -a /Library/Keychains/System.keychain",
            cancellationToken);

        return result.Success && result.StandardOutput.Contains("localhost");
    }

    private async Task<bool> CheckMacOSTrustSettingsAsync(CancellationToken cancellationToken)
    {
        // Check if cert is trusted in admin trust settings
        var result = await ProcessRunner.RunAsync(
            "security",
            "dump-trust-settings -d",
            cancellationToken);

        // If the cert is in the admin trust settings with trustAsRoot, it's trusted
        return result.StandardOutput.Contains("localhost") ||
               await CheckMacOSSystemKeychainAsync(cancellationToken);
    }

    private async Task<bool> ImportToMacOSSystemKeychainAsync(string pfxPath, string password, CancellationToken cancellationToken)
    {
        try
        {
            // Import the PFX to System keychain
            var result = await ProcessRunner.RunAsync(
                "sudo",
                $"security import \"{pfxPath}\" -k /Library/Keychains/System.keychain -P \"{password}\" -T /usr/bin/codesign -T /usr/bin/security",
                cancellationToken);

            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> TrustInMacOSSystemKeychainAsync(string pfxPath, string password, CancellationToken cancellationToken)
    {
        // First import to System keychain
        if (!await ImportToMacOSSystemKeychainAsync(pfxPath, password, cancellationToken))
        {
            return false;
        }

        try
        {
            // Export cert to temp file for trust settings
            var cert = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password);
            var tempCertPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cer");
            await File.WriteAllBytesAsync(tempCertPath, cert.Export(X509ContentType.Cert), cancellationToken);

            // Add trust settings for SSL
            var result = await ProcessRunner.RunAsync(
                "sudo",
                $"security add-trusted-cert -d -r trustAsRoot -k /Library/Keychains/System.keychain \"{tempCertPath}\"",
                cancellationToken);

            File.Delete(tempCertPath);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CleanMacOSSystemKeychainAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Delete the certificate from System keychain using -t flag to also remove trust settings
            var deleteResult = await ProcessRunner.RunAsync(
                "sudo",
                "security delete-certificate -t -c \"localhost\" /Library/Keychains/System.keychain",
                cancellationToken);

            // Success if either the cert was deleted or it didn't exist
            return deleteResult.Success || deleteResult.StandardError.Contains("could not be found");
        }
        catch
        {
            return false;
        }
    }

    private async Task<CertificateInfo?> GetMacOSCertificateInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var exportResult = await ProcessRunner.RunAsync(
                "security",
                $"find-certificate -c \"localhost\" -p /Library/Keychains/System.keychain",
                cancellationToken);

            if (!exportResult.Success || string.IsNullOrWhiteSpace(exportResult.StandardOutput))
            {
                return null;
            }

            // Parse PEM output
            var pemContent = exportResult.StandardOutput;
            var cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(
                pemContent
                    .Replace("-----BEGIN CERTIFICATE-----", "")
                    .Replace("-----END CERTIFICATE-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim()));

            // Check trust settings
            var isTrusted = await CheckMacOSTrustSettingsAsync(cancellationToken);

            return CertificateInfo.FromCertificate(cert, isTrusted);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
