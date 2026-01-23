using System.Runtime.InteropServices;
using DotnetDevCertsPlus.Services;
using NSubstitute;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class WslServiceTests
{
    private readonly IProcessRunner _mockRunner;
    private readonly WslService _service;

    public WslServiceTests()
    {
        _mockRunner = Substitute.For<IProcessRunner>();
        _service = new WslService(_mockRunner);
    }

    [Fact]
    public void ConvertToWslPath_CPath_ConvertsCorrectly()
    {
        // Act
        var result = _service.ConvertToWslPath(@"C:\Users\test\file.txt");

        // Assert
        Assert.Equal("/mnt/c/Users/test/file.txt", result);
    }

    [Fact]
    public void ConvertToWslPath_DPath_ConvertsCorrectly()
    {
        // Act
        var result = _service.ConvertToWslPath(@"D:\Projects\app\cert.pfx");

        // Assert
        Assert.Equal("/mnt/d/Projects/app/cert.pfx", result);
    }

    [Fact]
    public void ConvertToWslPath_LowercaseDrive_ConvertsToLowercase()
    {
        // Act
        var result = _service.ConvertToWslPath(@"E:\Data\file.txt");

        // Assert
        Assert.Equal("/mnt/e/Data/file.txt", result);
    }

    [Fact]
    public void ConvertToWslPath_NoDriveLetter_JustReplacesBackslashes()
    {
        // Act
        var result = _service.ConvertToWslPath(@"relative\path\file.txt");

        // Assert
        Assert.Equal("relative/path/file.txt", result);
    }

    [Fact]
    public void ConvertToWslPath_StaticMethod_AlsoWorks()
    {
        // Act
        var result = WslService.ConvertWindowsPathToWsl(@"C:\temp\cert.pfx");

        // Assert
        Assert.Equal("/mnt/c/temp/cert.pfx", result);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task GetDefaultDistroAsync_OnWindows_CallsWslList()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "--list --quiet", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Ubuntu\nDebian\n", ""));

        // Act
        var result = await _service.GetDefaultDistroAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Ubuntu", result);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task GetDefaultDistroAsync_NoDistros_ReturnsNull()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "--list --quiet", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", ""));

        // Act
        var result = await _service.GetDefaultDistroAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task CheckDotnetAvailableAsync_WhenAvailable_ReturnsTrue()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "-- which dotnet", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "/usr/bin/dotnet", ""));

        // Act
        var result = await _service.CheckDotnetAvailableAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task CheckDotnetAvailableAsync_WhenNotAvailable_ReturnsFalse()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "-- which dotnet", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "", ""));

        // Act
        var result = await _service.CheckDotnetAvailableAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task CheckDotnetAvailableAsync_WithDistro_UsesDistroArg()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "-d Ubuntu -- which dotnet", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "/usr/bin/dotnet", ""));

        // Act
        var result = await _service.CheckDotnetAvailableAsync("Ubuntu", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        await _mockRunner.Received(1).RunAsync("wsl.exe", "-d Ubuntu -- which dotnet", Arg.Any<CancellationToken>());
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task RunCommandAsync_ExecutesInWsl()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "-- echo hello", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "hello", ""));

        // Act
        var result = await _service.RunCommandAsync("echo hello", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("hello", result.StandardOutput);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task CheckCertStatusAsync_CertExistsAndTrusted_ReturnsCorrectStatus()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "-- dotnet dev-certs https --check --trust", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Certificate is valid", ""));

        // Act
        var (exists, trusted) = await _service.CheckCertStatusAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(exists);
        Assert.True(trusted);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task CheckCertStatusAsync_CertNotFound_ReturnsNotExists()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "-- dotnet dev-certs https --check --trust", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(1, "No valid certificate", ""));

        // Act
        var (exists, trusted) = await _service.CheckCertStatusAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.False(exists);
        Assert.False(trusted);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task ImportCertAsync_WithTrust_IncludesTrustFlag()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Imported", ""));

        // Act
        var result = await _service.ImportCertAsync(@"C:\temp\cert.pfx", "password", trust: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
        await _mockRunner.Received(1).RunAsync(
            "wsl.exe",
            Arg.Is<string>(s => s.Contains("--trust") && s.Contains("/mnt/c/temp/cert.pfx")),
            Arg.Any<CancellationToken>());
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task ImportCertAsync_UsesShellEscaping()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange - password with special characters that would be dangerous in shell
        _mockRunner.RunAsync("wsl.exe", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Imported", ""));

        // Act - use a path/password with dangerous shell chars
        var result = await _service.ImportCertAsync(@"C:\temp\cert.pfx", "pass'word", trust: false, cancellationToken: TestContext.Current.CancellationToken);

        // Assert - should use single quotes (shell-safe escaping)
        Assert.True(result);
        await _mockRunner.Received(1).RunAsync(
            "wsl.exe",
            Arg.Is<string>(s => s.Contains("'") && !s.Contains("\"")),  // single quotes used, not double
            Arg.Any<CancellationToken>());
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task CleanCertAsync_CallsCorrectCommand()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        _mockRunner.RunAsync("wsl.exe", "-- dotnet dev-certs https --clean", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Cleaned", ""));

        // Act
        var result = await _service.CleanCertAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void DefaultConstructor_CreatesInstance()
    {
        // Act
        var service = new WslService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact(Skip = "WSL tests only run on Windows")]
    [Trait("Category", "Windows")]
    public async Task GetDefaultDistroAsync_CleansNullCharacters()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange - WSL output sometimes has null chars
        _mockRunner.RunAsync("wsl.exe", "--list --quiet", Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "Ubuntu\0\n", ""));

        // Act
        var result = await _service.GetDefaultDistroAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Ubuntu", result);
        Assert.DoesNotContain("\0", result);
    }
}
