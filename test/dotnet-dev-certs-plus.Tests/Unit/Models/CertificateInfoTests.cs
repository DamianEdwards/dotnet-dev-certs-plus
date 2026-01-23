using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using DotnetDevCertsPlus.Models;

namespace DotnetDevCertsPlus.Tests.Unit.Models;

public class CertificateInfoTests
{
    [Fact]
    public void FromCertificate_WithTrustedCert_SetsFullTrustLevel()
    {
        // Arrange
        using var cert = CreateTestCertificate();

        // Act
        var info = CertificateInfo.FromCertificate(cert, isTrusted: true);

        // Assert
        Assert.Equal("Full", info.TrustLevel);
        Assert.Equal(cert.Thumbprint, info.Thumbprint);
        Assert.Equal(cert.Subject, info.Subject);
    }

    [Fact]
    public void FromCertificate_WithUntrustedCert_SetsNoneTrustLevel()
    {
        // Arrange
        using var cert = CreateTestCertificate();

        // Act
        var info = CertificateInfo.FromCertificate(cert, isTrusted: false);

        // Assert
        Assert.Equal("None", info.TrustLevel);
    }

    [Fact]
    public void FromCertificate_SetsValidityDates()
    {
        // Arrange
        using var cert = CreateTestCertificate();

        // Act
        var info = CertificateInfo.FromCertificate(cert, isTrusted: true);

        // Assert
        Assert.Equal(new DateTimeOffset(cert.NotBefore), info.ValidityNotBefore);
        Assert.Equal(new DateTimeOffset(cert.NotAfter), info.ValidityNotAfter);
    }

    [Fact]
    public void ToJson_SerializesCorrectly()
    {
        // Arrange
        var info = new CertificateInfo
        {
            Thumbprint = "ABC123",
            Subject = "CN=localhost",
            TrustLevel = "Full",
            Version = 3,
            IsHttpsDevelopmentCertificate = true,
            IsExportable = true,
            ValidityNotBefore = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ValidityNotAfter = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        // Act
        var json = CertificateInfo.ToJson([info]);

        // Assert
        Assert.Contains("ABC123", json);
        Assert.Contains("CN=localhost", json);
        Assert.Contains("Full", json);
        
        // Verify it's valid JSON
        var parsed = JsonDocument.Parse(json);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void ToJson_EmptyList_ReturnsEmptyArray()
    {
        // Act
        var json = CertificateInfo.ToJson([]);

        // Assert
        Assert.Equal("[]", json);
    }

    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddYears(1));
    }
}
