using DotnetDevCertsPlus.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class UpdateCheckerTests
{
    private readonly IUpdateStateManager _mockStateManager;
    private readonly INuGetClient _mockNuGetClient;
    private readonly IGitHubPackagesClient _mockGitHubClient;
    private readonly IVersionInfoProvider _mockVersionProvider;

    public UpdateCheckerTests()
    {
        _mockStateManager = Substitute.For<IUpdateStateManager>();
        _mockNuGetClient = Substitute.For<INuGetClient>();
        _mockGitHubClient = Substitute.For<IGitHubPackagesClient>();
        _mockVersionProvider = Substitute.For<IVersionInfoProvider>();
    }

    private UpdateChecker CreateChecker() => new UpdateChecker(
        _mockStateManager, 
        _mockNuGetClient, 
        _mockGitHubClient, 
        _mockVersionProvider, 
        NullLogger<UpdateChecker>.Instance);

    private void SetupVersion(string version)
    {
        _mockVersionProvider.GetCurrentVersion().Returns(version);
        _mockVersionProvider.GetCurrentBuildType().Returns(VersionInfo.GetBuildType(version));
    }

    #region CheckForUpdateAsync Tests

    [Fact]
    public async Task CheckForUpdateAsync_UpdatesLastCheckTime()
    {
        // Arrange
        SetupVersion("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        var checker = CreateChecker();

        // Act
        await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        _mockStateManager.Received(1).UpdateLastCheckTime();
    }

    [Fact]
    public async Task CheckForUpdateAsync_StableBuild_OnlyChecksNuGet()
    {
        // Arrange
        SetupVersion("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.1" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await _mockNuGetClient.Received(1).GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockGitHubClient.DidNotReceive().GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.0.1", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_DevBuild_ChecksBothSources()
    {
        // Arrange
        SetupVersion("1.0.0-pre.1.dev.1");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0" });
        _mockGitHubClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0-pre.1.dev.2" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await _mockNuGetClient.Received(1).GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockGitHubClient.Received(1).GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.True(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_FindsNewerVersion_SetsAvailableUpdate()
    {
        // Arrange
        SetupVersion("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0", "1.1.0", "2.0.0" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        _mockStateManager.Received(1).SetAvailableUpdate("2.0.0");
        Assert.Equal("2.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NoNewerVersion_ClearsAvailableUpdate()
    {
        // Arrange
        SetupVersion("2.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0", "1.1.0", "2.0.0" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        _mockStateManager.Received(1).ClearAvailableUpdate();
        Assert.False(result.UpdateAvailable);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_StableBuild_IgnoresPreReleaseVersions()
    {
        // Arrange
        SetupVersion("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0", "1.1.0-pre.1", "2.0.0-alpha" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.UpdateAvailable);
        _mockStateManager.Received(1).ClearAvailableUpdate();
    }

    [Fact]
    public async Task CheckForUpdateAsync_PreReleaseBuild_AcceptsStable()
    {
        // Arrange
        SetupVersion("1.0.0-pre.1.rel");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_PreReleaseBuild_OnlyChecksNuGet()
    {
        // Arrange
        SetupVersion("1.0.0-pre.1");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0-pre.2" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await _mockNuGetClient.Received(1).GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockGitHubClient.DidNotReceive().GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.True(result.UpdateAvailable);
        _mockStateManager.Received(1).SetAvailableUpdate("1.0.0-pre.2");
    }

    [Fact]
    public async Task CheckForUpdateAsync_PreReleaseBuild_AcceptsNewerPreRelease()
    {
        // Arrange
        SetupVersion("1.0.0-pre.1");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0-pre.2", "1.0.0-pre.3" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.0.0-pre.3", result.LatestVersion);
        _mockStateManager.Received(1).SetAvailableUpdate("1.0.0-pre.3");
    }

    [Fact]
    public async Task CheckForUpdateAsync_DevBuild_AcceptsStableFromNuGet()
    {
        // Arrange
        SetupVersion("1.0.0-pre.1.dev.1");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0" });
        _mockGitHubClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.0.0", result.LatestVersion);
        _mockStateManager.Received(1).SetAvailableUpdate("1.0.0");
    }

    [Fact]
    public async Task CheckForUpdateAsync_DevBuild_AcceptsPreReleaseFromNuGet()
    {
        // Arrange
        SetupVersion("1.0.0-pre.1.dev.1");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0-pre.2" });
        _mockGitHubClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.0.0-pre.2", result.LatestVersion);
        _mockStateManager.Received(1).SetAvailableUpdate("1.0.0-pre.2");
    }

    [Fact]
    public async Task CheckForUpdateAsync_DevBuild_AcceptsNewerDevFromGitHub()
    {
        // Arrange
        SetupVersion("1.0.0-pre.1.dev.1");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());
        _mockGitHubClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0-pre.1.dev.2", "1.0.0-pre.1.dev.3" });
        var checker = CreateChecker();

        // Act
        var result = await checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.0.0-pre.1.dev.3", result.LatestVersion);
        _mockStateManager.Received(1).SetAvailableUpdate("1.0.0-pre.1.dev.3");
    }

    #endregion

    #region GetCachedAvailableUpdate Tests

    [Fact]
    public void GetCachedAvailableUpdate_ReturnsStateManagerValue()
    {
        // Arrange
        _mockStateManager.GetAvailableUpdate().Returns("2.0.0");
        var checker = CreateChecker();

        // Act
        var result = checker.GetCachedAvailableUpdate();

        // Assert
        Assert.Equal("2.0.0", result);
    }

    [Fact]
    public void GetCachedAvailableUpdate_NoUpdate_ReturnsNull()
    {
        // Arrange
        _mockStateManager.GetAvailableUpdate().Returns((string?)null);
        var checker = CreateChecker();

        // Act
        var result = checker.GetCachedAvailableUpdate();

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ShouldStartBackgroundCheck Tests

    [Fact]
    public void ShouldStartBackgroundCheck_CheckNeeded_ReturnsTrue()
    {
        // Arrange
        _mockStateManager.ShouldCheckForUpdate(Arg.Any<TimeSpan>()).Returns(true);
        var checker = CreateChecker();

        // Act
        var result = checker.ShouldStartBackgroundCheck();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldStartBackgroundCheck_RecentCheck_ReturnsFalse()
    {
        // Arrange
        _mockStateManager.ShouldCheckForUpdate(Arg.Any<TimeSpan>()).Returns(false);
        var checker = CreateChecker();

        // Act
        var result = checker.ShouldStartBackgroundCheck();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldStartBackgroundCheck_Disabled_ReturnsFalse()
    {
        // Arrange
        _mockStateManager.ShouldCheckForUpdate(Arg.Any<TimeSpan>()).Returns(true);
        Environment.SetEnvironmentVariable("DOTNET_DEV_CERTS_PLUS_DISABLE_UPDATE_CHECK", "1");
        var checker = CreateChecker();
        try
        {
            // Act
            var result = checker.ShouldStartBackgroundCheck();

            // Assert
            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_DEV_CERTS_PLUS_DISABLE_UPDATE_CHECK", null);
        }
    }

    #endregion

    #region IsUpdateCheckDisabled Tests

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("", false)]
    public void IsUpdateCheckDisabled_RespectsEnvVar(string envValue, bool expected)
    {
        Environment.SetEnvironmentVariable("DOTNET_DEV_CERTS_PLUS_DISABLE_UPDATE_CHECK", envValue);
        var checker = CreateChecker();
        try
        {
            var result = checker.IsUpdateCheckDisabled();
            Assert.Equal(expected, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOTNET_DEV_CERTS_PLUS_DISABLE_UPDATE_CHECK", null);
        }
    }

    [Fact]
    public void IsUpdateCheckDisabled_NoEnvVar_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("DOTNET_DEV_CERTS_PLUS_DISABLE_UPDATE_CHECK", null);
        var checker = CreateChecker();
        
        var result = checker.IsUpdateCheckDisabled();
        
        Assert.False(result);
    }

    #endregion
}
