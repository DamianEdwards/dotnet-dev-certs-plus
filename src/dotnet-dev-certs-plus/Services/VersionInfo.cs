using NuGet.Versioning;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Represents the type of build based on version string analysis.
/// </summary>
public enum BuildType
{
    /// <summary>
    /// Development build (version contains "dev.")
    /// </summary>
    Dev,

    /// <summary>
    /// Pre-release build (version contains "-pre." but not "dev.")
    /// </summary>
    PreRelease,

    /// <summary>
    /// Stable release (no pre-release suffix)
    /// </summary>
    Stable
}

/// <summary>
/// Static utility methods for working with version strings.
/// </summary>
public static partial class VersionInfo
{

    /// <summary>
    /// Determines the build type based on the version string.
    /// </summary>
    public static BuildType GetBuildType(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return BuildType.Stable;
        }

        // Dev builds contain "dev." in the version
        if (version.Contains("dev.", StringComparison.OrdinalIgnoreCase))
        {
            return BuildType.Dev;
        }

        // Use NuGet.Versioning to check for pre-release
        if (NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return nugetVersion.IsPrerelease ? BuildType.PreRelease : BuildType.Stable;
        }

        // Fallback: check for hyphen (pre-release indicator)
        if (version.Contains('-'))
        {
            return BuildType.PreRelease;
        }

        return BuildType.Stable;
    }

    /// <summary>
    /// Compares two semantic versions using NuGet.Versioning. Returns:
    /// -1 if version1 &lt; version2
    ///  0 if version1 == version2
    ///  1 if version1 &gt; version2
    /// </summary>
    public static int CompareVersions(string version1, string version2)
    {
        if (string.IsNullOrEmpty(version1) && string.IsNullOrEmpty(version2))
        {
            return 0;
        }
        if (string.IsNullOrEmpty(version1))
        {
            return -1;
        }
        if (string.IsNullOrEmpty(version2))
        {
            return 1;
        }

        // Use NuGet.Versioning for proper semantic version comparison
        if (NuGetVersion.TryParse(version1, out var v1) && NuGetVersion.TryParse(version2, out var v2))
        {
            return v1.CompareTo(v2);
        }

        // Fallback to string comparison if parsing fails
        return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if newVersion is an update from currentVersion based on build type rules.
    /// </summary>
    public static bool IsUpdateAvailable(string currentVersion, string newVersion, BuildType currentBuildType)
    {
        if (string.IsNullOrEmpty(newVersion))
        {
            return false;
        }

        var comparison = CompareVersions(currentVersion, newVersion);
        if (comparison >= 0)
        {
            return false;
        }

        // For dev builds, any newer version is valid
        if (currentBuildType == BuildType.Dev)
        {
            return true;
        }

        var newBuildType = GetBuildType(newVersion);

        // For pre-release builds, show newer pre-release or stable
        if (currentBuildType == BuildType.PreRelease)
        {
            return newBuildType == BuildType.PreRelease || newBuildType == BuildType.Stable;
        }

        // For stable builds, only show newer stable
        return newBuildType == BuildType.Stable;
    }
}
