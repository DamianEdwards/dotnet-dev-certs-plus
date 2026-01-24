using System.Reflection;
using System.Runtime.InteropServices;
using DotnetDevCertsPlus.Services;
using NSubstitute;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class MachineStoreServiceTests
{
    private readonly IProcessRunner _mockRunner;
    private readonly MachineStoreService _service;

    public MachineStoreServiceTests()
    {
        _mockRunner = Substitute.For<IProcessRunner>();
        _service = new MachineStoreService(_mockRunner);
    }

    [Fact]
    public void CertFriendlyName_HasExpectedValue()
    {
        // The friendly name should match what dotnet dev-certs https uses
        var expectedFriendlyName = "ASP.NET Core HTTPS development certificate";

        // Use reflection to verify the constant value
        var field = typeof(MachineStoreService)
            .GetField("CertFriendlyName", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var actualValue = field.GetValue(null) as string;
        Assert.Equal(expectedFriendlyName, actualValue);
    }

    [Fact]
    public void DefaultConstructor_UsesDefaultProcessRunner()
    {
        // Act
        var service = new MachineStoreService();

        // Assert - just verify it doesn't throw
        Assert.NotNull(service);
    }

    [Fact]
    public async Task CheckMachineStoreAsync_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        // This test verifies behavior on unsupported platforms
        // On Windows/Linux/macOS it will exercise those code paths instead
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Skip on supported platforms - the actual behavior is tested elsewhere
            return;
        }

        // Act & Assert
        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => _service.CheckMachineStoreAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckMachineTrustAsync_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => _service.CheckMachineTrustAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ImportToMachineStoreAsync_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => _service.ImportToMachineStoreAsync("/path/to/cert.pfx", "password", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TrustInMachineStoreAsync_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => _service.TrustInMachineStoreAsync("/path/to/cert.pfx", "password", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CleanMachineStoreAsync_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => _service.CleanMachineStoreAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCertificateInfoAsync_OnUnsupportedPlatform_ThrowsPlatformNotSupportedException()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => _service.GetCertificateInfoAsync(TestContext.Current.CancellationToken));
    }
}
