using DotnetDevCertsPlus.Commands;
using DotnetDevCertsPlus.Services;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Commands;

public class HttpsCommandTests
{
    #region GetUpdateCommand(BuildType) Tests

    [Fact]
    public void GetUpdateCommand_DevBuildType_IncludesGitHubPackagesSourceAndPrereleaseFlag()
    {
        var command = HttpsCommand.GetUpdateCommand(BuildType.Dev);

        Assert.Contains("--add-source https://nuget.pkg.github.com/DamianEdwards/index.json", command);
        Assert.Contains("--prerelease", command);
        Assert.Contains("dotnet tool update -g dotnet-dev-certs-plus", command);
    }

    [Fact]
    public void GetUpdateCommand_PreReleaseBuildType_IncludesPrereleaseFlag()
    {
        var command = HttpsCommand.GetUpdateCommand(BuildType.PreRelease);

        Assert.Contains("--prerelease", command);
        Assert.Contains("dotnet tool update -g dotnet-dev-certs-plus", command);
        Assert.DoesNotContain("--add-source", command);
    }

    [Fact]
    public void GetUpdateCommand_StableBuildType_BasicCommand()
    {
        var command = HttpsCommand.GetUpdateCommand(BuildType.Stable);

        Assert.Equal("dotnet tool update -g dotnet-dev-certs-plus", command);
        Assert.DoesNotContain("--prerelease", command);
        Assert.DoesNotContain("--add-source", command);
    }

    [Theory]
    [InlineData(BuildType.Dev)]
    [InlineData(BuildType.PreRelease)]
    [InlineData(BuildType.Stable)]
    public void GetUpdateCommand_AllBuildTypes_ContainsBaseCommand(BuildType buildType)
    {
        var command = HttpsCommand.GetUpdateCommand(buildType);

        Assert.StartsWith("dotnet tool update -g dotnet-dev-certs-plus", command);
    }

    #endregion

    #region GetUpdateCommand(string targetVersion) Tests

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.5.3")]
    [InlineData("0.0.1")]
    public void GetUpdateCommand_StableTargetVersion_NoPreleaseFlag(string targetVersion)
    {
        var command = HttpsCommand.GetUpdateCommand(targetVersion);

        Assert.Equal("dotnet tool update -g dotnet-dev-certs-plus", command);
        Assert.DoesNotContain("--prerelease", command);
    }

    [Theory]
    [InlineData("1.0.0-pre.1")]
    [InlineData("2.0.0-beta.5")]
    [InlineData("0.1.0-alpha")]
    public void GetUpdateCommand_PreReleaseTargetVersion_IncludesPrereleaseFlag(string targetVersion)
    {
        var command = HttpsCommand.GetUpdateCommand(targetVersion);

        Assert.Contains("--prerelease", command);
        Assert.DoesNotContain("--add-source", command);
    }

    [Theory]
    [InlineData("1.0.0-dev.123")]
    [InlineData("0.0.1-dev.1+abc123")]
    public void GetUpdateCommand_DevTargetVersion_IncludesGitHubSourceAndPrereleaseFlag(string targetVersion)
    {
        var command = HttpsCommand.GetUpdateCommand(targetVersion);

        Assert.Contains("--prerelease", command);
        Assert.Contains("--add-source https://nuget.pkg.github.com/DamianEdwards/index.json", command);
    }

    #endregion
}
