using DotnetDevCertsPlus.Services;
using NSubstitute;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class UpdateCheckerTests : IDisposable
{
    private readonly IUpdateStateManager _mockStateManager;
    private readonly INuGetClient _mockNuGetClient;
    private readonly IGitHubPackagesClient _mockGitHubClient;
    private readonly UpdateChecker _checker;

    public UpdateCheckerTests()
    {
        _mockStateManager = Substitute.For<IUpdateStateManager>();
        _mockNuGetClient = Substitute.For<INuGetClient>();
        _mockGitHubClient = Substitute.For<IGitHubPackagesClient>();
        _checker = new UpdateChecker(_mockStateManager, _mockNuGetClient, _mockGitHubClient, NullUpdateLogger.Instance);

        // Reset version for each test
        VersionInfo.SetVersionForTesting(null);
    }

    public void Dispose()
    {
        VersionInfo.SetVersionForTesting(null);
        GC.SuppressFinalize(this);
    }

    #region CheckForUpdateAsync Tests

    [Fact]
    public async Task CheckForUpdateAsync_UpdatesLastCheckTime()
    {
        // Arrange
        VersionInfo.SetVersionForTesting("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        // Act
        await _checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        _mockStateManager.Received(1).UpdateLastCheckTime();
    }

    [Fact]
    public async Task CheckForUpdateAsync_StableBuild_OnlyChecksNuGet()
    {
        // Arrange
        VersionInfo.SetVersionForTesting("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.1" });

        // Act
        var result = await _checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

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
        VersionInfo.SetVersionForTesting("1.0.0-pre.1.dev.1");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0" });
        _mockGitHubClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0-pre.1.dev.2" });

        // Act
        var result = await _checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        await _mockNuGetClient.Received(1).GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mockGitHubClient.Received(1).GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.True(result.UpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_FindsNewerVersion_SetsAvailableUpdate()
    {
        // Arrange
        VersionInfo.SetVersionForTesting("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0", "1.1.0", "2.0.0" });

        // Act
        var result = await _checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        _mockStateManager.Received(1).SetAvailableUpdate("2.0.0");
        Assert.Equal("2.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NoNewerVersion_ClearsAvailableUpdate()
    {
        // Arrange
        VersionInfo.SetVersionForTesting("2.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0", "1.1.0", "2.0.0" });

        // Act
        var result = await _checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        _mockStateManager.Received(1).ClearAvailableUpdate();
        Assert.False(result.UpdateAvailable);
        Assert.Null(result.LatestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_StableBuild_IgnoresPreReleaseVersions()
    {
        // Arrange
        VersionInfo.SetVersionForTesting("1.0.0");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0", "1.1.0-pre.1", "2.0.0-alpha" });

        // Act
        var result = await _checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.UpdateAvailable);
        _mockStateManager.Received(1).ClearAvailableUpdate();
    }

    [Fact]
    public async Task CheckForUpdateAsync_PreReleaseBuild_AcceptsStable()
    {
        // Arrange
        VersionInfo.SetVersionForTesting("1.0.0-pre.1.rel");
        _mockNuGetClient.GetPackageVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new List<string> { "1.0.0" });

        // Act
        var result = await _checker.CheckForUpdateAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.UpdateAvailable);
        Assert.Equal("1.0.0", result.LatestVersion);
    }

    #endregion

    #region GetCachedAvailableUpdate Tests

    [Fact]
    public void GetCachedAvailableUpdate_ReturnsStateManagerValue()
    {
        // Arrange
        _mockStateManager.GetAvailableUpdate().Returns("2.0.0");

        // Act
        var result = _checker.GetCachedAvailableUpdate();

        // Assert
        Assert.Equal("2.0.0", result);
    }

    [Fact]
    public void GetCachedAvailableUpdate_NoUpdate_ReturnsNull()
    {
        // Arrange
        _mockStateManager.GetAvailableUpdate().Returns((string?)null);

        // Act
        var result = _checker.GetCachedAvailableUpdate();

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

        // Act
        var result = _checker.ShouldStartBackgroundCheck();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldStartBackgroundCheck_RecentCheck_ReturnsFalse()
    {
        // Arrange
        _mockStateManager.ShouldCheckForUpdate(Arg.Any<TimeSpan>()).Returns(false);

        // Act
        var result = _checker.ShouldStartBackgroundCheck();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldStartBackgroundCheck_Disabled_ReturnsFalse()
    {
        // Arrange
        _mockStateManager.ShouldCheckForUpdate(Arg.Any<TimeSpan>()).Returns(true);
        Environment.SetEnvironmentVariable("DOTNET_DEV_CERTS_PLUS_DISABLE_UPDATE_CHECK", "1");
        try
        {
            // Act
            var result = _checker.ShouldStartBackgroundCheck();

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
        try
        {
            var result = _checker.IsUpdateCheckDisabled();
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
        
        var result = _checker.IsUpdateCheckDisabled();
        
        Assert.False(result);
    }

    #endregion
}
