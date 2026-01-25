namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Provides version information for the application.
/// </summary>
public interface IVersionInfoProvider
{
    /// <summary>
    /// Gets the current application version.
    /// </summary>
    string GetCurrentVersion();

    /// <summary>
    /// Gets the build type of the current version.
    /// </summary>
    BuildType GetCurrentBuildType();
}
