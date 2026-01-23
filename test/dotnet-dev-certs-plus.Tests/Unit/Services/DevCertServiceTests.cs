using DotnetDevCertsPlus.Services;
using NSubstitute;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class DevCertServiceTests
{
    private readonly IProcessRunner _mockRunner;
    private readonly DevCertService _service;

    public DevCertServiceTests()
    {
        _mockRunner = Substitute.For<IProcessRunner>();
        _service = new DevCertService(_mockRunner);
    }

    [Fact]
    public async Task CheckStatusAsync_CallsDotnetDevCertsWithCorrectArgs()
    {
        // Arrange
        _mockRunner.RunAsync("dotnet", "dev-certs https --check --trust", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Certificate is valid", ""));

        // Act
        var result = await _service.CheckStatusAsync();

        // Assert
        Assert.True(result.Exists);
        Assert.True(result.IsTrusted);
        await _mockRunner.Received(1).RunAsync("dotnet", "dev-certs https --check --trust", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckStatusAsync_ExitCode2_ReturnsNotExists()
    {
        // Arrange
        _mockRunner.RunAsync("dotnet", "dev-certs https --check --trust", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(2, "No valid certificate found", ""));

        // Act
        var result = await _service.CheckStatusAsync();

        // Assert
        Assert.False(result.Exists);
        Assert.False(result.IsTrusted);
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenCertExists_DoesNotCreateNew()
    {
        // Arrange
        _mockRunner.RunAsync("dotnet", "dev-certs https --check --trust", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Certificate is valid", ""));

        // Act
        var result = await _service.EnsureCreatedAsync();

        // Assert
        Assert.True(result);
        // Should only call check, not create
        await _mockRunner.Received(1).RunAsync("dotnet", "dev-certs https --check --trust", Arg.Any<CancellationToken>());
        await _mockRunner.DidNotReceive().RunAsync("dotnet", "dev-certs https", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenCertNotExists_CreatesNew()
    {
        // Arrange
        _mockRunner.RunAsync("dotnet", "dev-certs https --check --trust", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(2, "No valid certificate found", ""));
        _mockRunner.RunAsync("dotnet", "dev-certs https", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Certificate created", ""));

        // Act
        var result = await _service.EnsureCreatedAsync();

        // Assert
        Assert.True(result);
        await _mockRunner.Received(1).RunAsync("dotnet", "dev-certs https", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureCreatedAsync_WhenCreateFails_ReturnsFalse()
    {
        // Arrange
        _mockRunner.RunAsync("dotnet", "dev-certs https --check --trust", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(2, "No valid certificate found", ""));
        _mockRunner.RunAsync("dotnet", "dev-certs https", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "Failed to create certificate"));

        // Act
        var result = await _service.EnsureCreatedAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExportAsync_Pfx_IncludesPassword()
    {
        // Arrange
        var path = "/tmp/cert.pfx";
        var password = "test-password";
        _mockRunner.RunAsync("dotnet", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Exported", ""));

        // Act
        var result = await _service.ExportAsync(path, CertificateFormat.Pfx, password);

        // Assert
        Assert.True(result);
        await _mockRunner.Received(1).RunAsync(
            "dotnet",
            Arg.Is<string>(s => s.Contains("--export-path") && s.Contains("--password") && s.Contains("Pfx")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_Pem_DoesNotIncludePassword()
    {
        // Arrange
        var path = "/tmp/cert.pem";
        _mockRunner.RunAsync("dotnet", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Exported", ""));

        // Act
        var result = await _service.ExportAsync(path, CertificateFormat.Pem, null);

        // Assert
        Assert.True(result);
        await _mockRunner.Received(1).RunAsync(
            "dotnet",
            Arg.Is<string>(s => s.Contains("--export-path") && s.Contains("Pem") && !s.Contains("--password")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_WhenFails_ReturnsFalse()
    {
        // Arrange
        _mockRunner.RunAsync("dotnet", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", "Export failed"));

        // Act
        var result = await _service.ExportAsync("/tmp/cert.pfx", CertificateFormat.Pfx, "password");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void DefaultConstructor_UsesDefaultProcessRunner()
    {
        // Act
        var service = new DevCertService();

        // Assert - just verify it doesn't throw
        Assert.NotNull(service);
    }
}
