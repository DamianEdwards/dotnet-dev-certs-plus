using System.Reflection;
using System.Text.RegularExpressions;

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
/// Utilities for working with version information.
/// </summary>
public static partial class VersionInfo
{
    private static string? _cachedVersion;

    /// <summary>
    /// Gets the current assembly's informational version.
    /// </summary>
    public static string GetCurrentVersion()
    {
        if (_cachedVersion is not null)
        {
            return _cachedVersion;
        }

        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // Strip off any +commit hash suffix
        if (version is not null)
        {
            var plusIndex = version.IndexOf('+');
            if (plusIndex > 0)
            {
                version = version[..plusIndex];
            }
        }

        _cachedVersion = version ?? "0.0.0";
        return _cachedVersion;
    }

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

        // Pre-release builds have a hyphen followed by pre-release identifiers
        if (version.Contains('-'))
        {
            return BuildType.PreRelease;
        }

        return BuildType.Stable;
    }

    /// <summary>
    /// Compares two semantic versions. Returns:
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

        // Parse into base version and prerelease parts
        var (base1, pre1) = ParseVersion(version1);
        var (base2, pre2) = ParseVersion(version2);

        // Compare base versions first
        var baseComparison = CompareBaseVersions(base1, base2);
        if (baseComparison != 0)
        {
            return baseComparison;
        }

        // If base versions are equal, compare prerelease
        // No prerelease > any prerelease (stable is higher than pre-release)
        if (string.IsNullOrEmpty(pre1) && string.IsNullOrEmpty(pre2))
        {
            return 0;
        }
        if (string.IsNullOrEmpty(pre1))
        {
            return 1; // version1 is stable, version2 is prerelease
        }
        if (string.IsNullOrEmpty(pre2))
        {
            return -1; // version1 is prerelease, version2 is stable
        }

        // Both have prerelease, compare them
        return ComparePrereleaseVersions(pre1, pre2);
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
            // Current version is same or newer
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

    private static (string baseVersion, string prerelease) ParseVersion(string version)
    {
        // Strip +metadata suffix if present
        var plusIndex = version.IndexOf('+');
        if (plusIndex > 0)
        {
            version = version[..plusIndex];
        }

        var hyphenIndex = version.IndexOf('-');
        if (hyphenIndex < 0)
        {
            return (version, string.Empty);
        }

        return (version[..hyphenIndex], version[(hyphenIndex + 1)..]);
    }

    private static int CompareBaseVersions(string base1, string base2)
    {
        var parts1 = base1.Split('.');
        var parts2 = base2.Split('.');

        var maxLength = Math.Max(parts1.Length, parts2.Length);
        for (var i = 0; i < maxLength; i++)
        {
            var num1 = i < parts1.Length && int.TryParse(parts1[i], out var n1) ? n1 : 0;
            var num2 = i < parts2.Length && int.TryParse(parts2[i], out var n2) ? n2 : 0;

            if (num1 < num2) return -1;
            if (num1 > num2) return 1;
        }

        return 0;
    }

    private static int ComparePrereleaseVersions(string pre1, string pre2)
    {
        // Split by dots and compare each segment
        var segments1 = pre1.Split('.');
        var segments2 = pre2.Split('.');

        var maxLength = Math.Max(segments1.Length, segments2.Length);
        for (var i = 0; i < maxLength; i++)
        {
            var seg1 = i < segments1.Length ? segments1[i] : string.Empty;
            var seg2 = i < segments2.Length ? segments2[i] : string.Empty;

            // Try to parse as numbers
            var isNum1 = int.TryParse(seg1, out var num1);
            var isNum2 = int.TryParse(seg2, out var num2);

            if (isNum1 && isNum2)
            {
                if (num1 < num2) return -1;
                if (num1 > num2) return 1;
            }
            else if (isNum1)
            {
                return -1; // Numeric < alpha
            }
            else if (isNum2)
            {
                return 1; // Alpha > numeric
            }
            else
            {
                var strCompare = string.Compare(seg1, seg2, StringComparison.OrdinalIgnoreCase);
                if (strCompare != 0) return strCompare;
            }
        }

        return 0;
    }

    /// <summary>
    /// For testing: allows setting a mock version.
    /// </summary>
    internal static void SetVersionForTesting(string? version)
    {
        _cachedVersion = version;
    }
}
