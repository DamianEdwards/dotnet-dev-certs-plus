using DotnetDevCertsPlus.Models;

namespace DotnetDevCertsPlus.Tests.Unit.Models;

public class DevCertStatusTests
{
    [Theory]
    [InlineData(0, "", true, true)]
    [InlineData(0, "Certificate is valid", true, true)]
    [InlineData(1, "Certificate exists but not trusted", true, false)]
    [InlineData(2, "No valid certificate found", false, false)]
    [InlineData(2, "", false, false)]
    [InlineData(1, "not trusted", true, false)]
    public void Parse_ReturnsCorrectStatus(int exitCode, string output, bool expectedExists, bool expectedTrusted)
    {
        // Act
        var result = DevCertStatus.Parse(output, exitCode);

        // Assert
        Assert.Equal(expectedExists, result.Exists);
        Assert.Equal(expectedTrusted, result.IsTrusted);
    }

    [Fact]
    public void Parse_ExitCode0_WithNoTrustedOutput_ReturnsTrusted()
    {
        // Exit code 0 means trusted, even if output doesn't say so explicitly
        var result = DevCertStatus.Parse("Some random output", 0);
        
        Assert.True(result.Exists);
        Assert.True(result.IsTrusted);
    }

    [Fact]
    public void Parse_ExitCode0_WithNotTrusted_ReturnsNotTrusted()
    {
        // If "not trusted" appears in output, should be not trusted even with exit code 0
        var result = DevCertStatus.Parse("Certificate is not trusted", 0);
        
        Assert.True(result.Exists);
        Assert.False(result.IsTrusted);
    }

    [Fact]
    public void Parse_ExitCode1_WithNoValidCertificate_ReturnsNotExists()
    {
        var result = DevCertStatus.Parse("No valid certificate found in the store", 1);
        
        Assert.False(result.Exists);
        Assert.False(result.IsTrusted);
    }
}
