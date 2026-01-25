using DotnetDevCertsPlus.Commands;
using DotnetDevCertsPlus.Services;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Commands;

public class HttpsCommandTests
{
    #region GetUpdateCommand Tests

    [Fact]
    public void GetUpdateCommand_DevBuild_IncludesGitHubPackagesSource()
    {
        var command = HttpsCommand.GetUpdateCommand(BuildType.Dev);

        Assert.Contains("--add-source https://nuget.pkg.github.com/DamianEdwards/index.json", command);
        Assert.Contains("dotnet tool update -g dotnet-dev-certs-plus", command);
    }

    [Fact]
    public void GetUpdateCommand_PreReleaseBuild_IncludesPrereleaseFlag()
    {
        var command = HttpsCommand.GetUpdateCommand(BuildType.PreRelease);

        Assert.Contains("--prerelease", command);
        Assert.Contains("dotnet tool update -g dotnet-dev-certs-plus", command);
        Assert.DoesNotContain("--add-source", command);
    }

    [Fact]
    public void GetUpdateCommand_StableBuild_BasicCommand()
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
}
