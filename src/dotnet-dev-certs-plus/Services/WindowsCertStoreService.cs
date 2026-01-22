using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Service for Windows machine certificate store operations.
/// </summary>
public class WindowsCertStoreService
{
    private const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
    private const string AspNetHttpsOidFriendlyName = "ASP.NET Core HTTPS development certificate";

    /// <summary>
    /// Checks if the dev certificate exists in the LocalMachine\My store.
    /// </summary>
    public bool CheckMachineStore()
    {
        EnsureWindows();

        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        return FindDevCert(store) is not null;
    }

    /// <summary>
    /// Checks if the dev certificate is trusted in the LocalMachine\Root store.
    /// </summary>
    public bool CheckMachineTrust()
    {
        EnsureWindows();

        using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);

        return FindDevCert(store) is not null;
    }

    /// <summary>
    /// Imports the dev certificate with private key to LocalMachine\My store.
    /// </summary>
    public bool ImportToMachineStore(string pfxPath, string password)
    {
        EnsureWindows();

        try
        {
            var cert = X509CertificateLoader.LoadPkcs12FromFile(
                pfxPath,
                password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            // Remove existing cert if present
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

    /// <summary>
    /// Adds the dev certificate to LocalMachine\Root store for trust (without private key).
    /// </summary>
    public bool TrustInMachineStore(string pfxPath, string password)
    {
        EnsureWindows();

        try
        {
            // Load the cert and export only the public key portion
            var certWithKey = X509CertificateLoader.LoadPkcs12FromFile(pfxPath, password);
            var publicCertBytes = certWithKey.Export(X509ContentType.Cert);
            var publicCert = X509CertificateLoader.LoadCertificate(publicCertBytes);

            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            // Remove existing cert if present
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

    /// <summary>
    /// Removes the dev certificate from the LocalMachine\My store.
    /// </summary>
    public bool RemoveFromMachineStore()
    {
        EnsureWindows();

        try
        {
            using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            var cert = FindDevCert(store);
            if (cert is not null)
            {
                store.Remove(cert);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes the dev certificate from the LocalMachine\Root store (untrust).
    /// </summary>
    public bool RemoveFromMachineTrust()
    {
        EnsureWindows();

        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            var cert = FindDevCert(store);
            if (cert is not null)
            {
                store.Remove(cert);
            }

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

    private static void EnsureWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Machine store operations are only supported on Windows.");
        }
    }
}
