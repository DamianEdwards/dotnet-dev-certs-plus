namespace DotnetDevCertsPlus.Models;

/// <summary>
/// Represents the status of the dev certificate as reported by dotnet dev-certs https --check --trust.
/// </summary>
public record DevCertStatus(bool Exists, bool IsTrusted)
{
    /// <summary>
    /// Parses the machine-readable output from dotnet dev-certs https --check --trust.
    /// Expected format includes lines like:
    /// - "Certificate exists: true/false"
    /// - "Certificate is trusted: true/false"
    /// </summary>
    public static DevCertStatus Parse(string output, int exitCode)
    {
        // Exit code 0 = cert exists and is trusted
        // Exit code 1 = cert exists but not trusted, or other error
        // Exit code 2 = cert does not exist

        var exists = exitCode != 2 && !output.Contains("No valid certificate found", StringComparison.OrdinalIgnoreCase);
        var isTrusted = exitCode == 0 && !output.Contains("not trusted", StringComparison.OrdinalIgnoreCase);

        return new DevCertStatus(exists, isTrusted);
    }
}
