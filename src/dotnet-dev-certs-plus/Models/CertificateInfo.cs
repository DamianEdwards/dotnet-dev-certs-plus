using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotnetDevCertsPlus.Models;

/// <summary>
/// Represents certificate information in the same format as dotnet dev-certs https --check-trust-machine-readable.
/// </summary>
public class CertificateInfo
{
    private const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";

    public string Thumbprint { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public List<string> X509SubjectAlternativeNameExtension { get; set; } = [];
    public int Version { get; set; }
    public DateTimeOffset ValidityNotBefore { get; set; }
    public DateTimeOffset ValidityNotAfter { get; set; }
    public bool IsHttpsDevelopmentCertificate { get; set; }
    public bool IsExportable { get; set; }
    public string TrustLevel { get; set; } = "None";

    /// <summary>
    /// Creates a CertificateInfo from an X509Certificate2.
    /// </summary>
    public static CertificateInfo FromCertificate(X509Certificate2 cert, bool isTrusted)
    {
        var info = new CertificateInfo
        {
            Thumbprint = cert.Thumbprint,
            Subject = cert.Subject,
            Version = cert.Version,
            ValidityNotBefore = new DateTimeOffset(cert.NotBefore),
            ValidityNotAfter = new DateTimeOffset(cert.NotAfter),
            IsHttpsDevelopmentCertificate = IsAspNetDevCert(cert),
            IsExportable = true, // Assume exportable for machine store certs
            TrustLevel = isTrusted ? "Full" : "None"
        };

        // Extract Subject Alternative Names
        foreach (var extension in cert.Extensions)
        {
            if (extension is X509SubjectAlternativeNameExtension sanExtension)
            {
                foreach (var name in sanExtension.EnumerateDnsNames())
                {
                    info.X509SubjectAlternativeNameExtension.Add(name);
                }
            }
        }

        return info;
    }

    /// <summary>
    /// Serializes a list of CertificateInfo to JSON matching dotnet dev-certs format.
    /// </summary>
    public static string ToJson(IEnumerable<CertificateInfo> certificates)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = null // Keep PascalCase to match dotnet dev-certs output
        };
        return JsonSerializer.Serialize(certificates, options);
    }

    private static bool IsAspNetDevCert(X509Certificate2 cert)
    {
        foreach (var extension in cert.Extensions)
        {
            if (extension.Oid?.Value == AspNetHttpsOid)
            {
                return true;
            }
        }
        return false;
    }
}
