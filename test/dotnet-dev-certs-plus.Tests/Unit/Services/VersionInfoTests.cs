using DotnetDevCertsPlus.Services;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class VersionInfoTests
{
    #region GetBuildType Tests

    [Theory]
    [InlineData("0.0.1-pre.1.dev.5", BuildType.Dev)]
    [InlineData("0.0.1-rtm.dev.3", BuildType.Dev)]
    [InlineData("1.0.0-pre.2.dev.1", BuildType.Dev)]
    public void GetBuildType_WithDevVersion_ReturnsDev(string version, BuildType expected)
    {
        var result = VersionInfo.GetBuildType(version);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.0.1-pre.1.rel", BuildType.PreRelease)]
    [InlineData("1.0.0-alpha", BuildType.PreRelease)]
    [InlineData("2.0.0-beta.1", BuildType.PreRelease)]
    [InlineData("0.0.1-rc.1", BuildType.PreRelease)]
    public void GetBuildType_WithPreReleaseVersion_ReturnsPreRelease(string version, BuildType expected)
    {
        var result = VersionInfo.GetBuildType(version);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0.0.1", BuildType.Stable)]
    [InlineData("1.0.0", BuildType.Stable)]
    [InlineData("2.3.4", BuildType.Stable)]
    [InlineData("", BuildType.Stable)]
    public void GetBuildType_WithStableVersion_ReturnsStable(string version, BuildType expected)
    {
        var result = VersionInfo.GetBuildType(version);
        Assert.Equal(expected, result);
    }

    #endregion

    #region CompareVersions Tests

    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("0.0.1", "0.0.1", 0)]
    [InlineData("1.2.3", "1.2.3", 0)]
    public void CompareVersions_EqualVersions_ReturnsZero(string v1, string v2, int expected)
    {
        var result = VersionInfo.CompareVersions(v1, v2);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("0.0.1", "0.0.2", -1)]
    [InlineData("0.1.0", "0.2.0", -1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    public void CompareVersions_FirstLower_ReturnsNegative(string v1, string v2, int expected)
    {
        var result = VersionInfo.CompareVersions(v1, v2);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2.0.0", "1.0.0", 1)]
    [InlineData("0.0.2", "0.0.1", 1)]
    [InlineData("0.2.0", "0.1.0", 1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    public void CompareVersions_FirstHigher_ReturnsPositive(string v1, string v2, int expected)
    {
        var result = VersionInfo.CompareVersions(v1, v2);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0-pre.1", 1)]  // Stable > pre-release
    [InlineData("1.0.0-pre.1", "1.0.0", -1)] // Pre-release < stable
    public void CompareVersions_StableVsPreRelease_StableIsHigher(string v1, string v2, int expected)
    {
        var result = VersionInfo.CompareVersions(v1, v2);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0-pre.1", "1.0.0-pre.2", -1)]
    [InlineData("1.0.0-pre.2", "1.0.0-pre.1", 1)]
    [InlineData("1.0.0-pre.1.dev.1", "1.0.0-pre.1.dev.2", -1)]
    [InlineData("1.0.0-pre.1.dev.2", "1.0.0-pre.1.dev.1", 1)]
    public void CompareVersions_PreReleaseVersions_ComparesCorrectly(string v1, string v2, int expected)
    {
        var result = VersionInfo.CompareVersions(v1, v2);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1.0.0+abc123", "1.0.0+def456", 0)]  // Metadata is ignored
    [InlineData("1.0.0+abc", "1.0.0", 0)]
    public void CompareVersions_IgnoresBuildMetadata(string v1, string v2, int expected)
    {
        var result = VersionInfo.CompareVersions(v1, v2);
        Assert.Equal(expected, result);
    }

    #endregion

    #region IsUpdateAvailable Tests

    [Fact]
    public void IsUpdateAvailable_SameVersion_ReturnsFalse()
    {
        var result = VersionInfo.IsUpdateAvailable("1.0.0", "1.0.0", BuildType.Stable);
        Assert.False(result);
    }

    [Fact]
    public void IsUpdateAvailable_LowerVersion_ReturnsFalse()
    {
        var result = VersionInfo.IsUpdateAvailable("2.0.0", "1.0.0", BuildType.Stable);
        Assert.False(result);
    }

    [Fact]
    public void IsUpdateAvailable_EmptyNewVersion_ReturnsFalse()
    {
        var result = VersionInfo.IsUpdateAvailable("1.0.0", "", BuildType.Stable);
        Assert.False(result);
    }

    // Dev build rules: any newer version is valid
    [Theory]
    [InlineData("0.0.1-pre.1.dev.1", "0.0.1-pre.1.dev.2")]  // Newer dev
    [InlineData("0.0.1-pre.1.dev.5", "0.0.1-pre.1.rel")]    // Pre-release
    [InlineData("0.0.1-pre.1.dev.5", "0.0.1")]              // Stable
    [InlineData("0.0.1-pre.1.dev.5", "0.0.2-pre.1.dev.1")]  // Newer base version
    public void IsUpdateAvailable_DevBuild_AcceptsAnyNewerVersion(string current, string newVersion)
    {
        var result = VersionInfo.IsUpdateAvailable(current, newVersion, BuildType.Dev);
        Assert.True(result);
    }

    // Pre-release rules: newer pre-release or stable
    [Theory]
    [InlineData("0.0.1-pre.1.rel", "0.0.1-pre.2.rel")]  // Newer pre-release
    [InlineData("0.0.1-pre.1.rel", "0.0.1")]            // Stable
    [InlineData("0.0.1-pre.1.rel", "0.0.2")]            // Newer stable
    public void IsUpdateAvailable_PreReleaseBuild_AcceptsNewerPreReleaseOrStable(string current, string newVersion)
    {
        var result = VersionInfo.IsUpdateAvailable(current, newVersion, BuildType.PreRelease);
        Assert.True(result);
    }

    [Theory]
    [InlineData("0.0.1-pre.1.rel", "0.0.1-pre.1.dev.5")]  // Dev builds not shown for pre-release
    public void IsUpdateAvailable_PreReleaseBuild_RejectsDevBuilds(string current, string newVersion)
    {
        var result = VersionInfo.IsUpdateAvailable(current, newVersion, BuildType.PreRelease);
        Assert.False(result);
    }

    // Stable rules: only newer stable
    [Theory]
    [InlineData("0.0.1", "0.0.2")]   // Newer stable
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.0", "2.0.0")]
    public void IsUpdateAvailable_StableBuild_AcceptsNewerStable(string current, string newVersion)
    {
        var result = VersionInfo.IsUpdateAvailable(current, newVersion, BuildType.Stable);
        Assert.True(result);
    }

    [Theory]
    [InlineData("0.0.1", "0.0.2-pre.1")]       // Pre-release not shown
    [InlineData("0.0.1", "0.0.2-pre.1.dev.1")] // Dev not shown
    public void IsUpdateAvailable_StableBuild_RejectsNonStable(string current, string newVersion)
    {
        var result = VersionInfo.IsUpdateAvailable(current, newVersion, BuildType.Stable);
        Assert.False(result);
    }

    #endregion
}
