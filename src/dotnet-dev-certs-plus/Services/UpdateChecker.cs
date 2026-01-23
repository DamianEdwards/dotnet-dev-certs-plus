using System.Diagnostics;

namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for the update checking service.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Performs an update check and updates state files.
    /// This is called by the background process.
    /// </summary>
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available update version from state files, if any.
    /// </summary>
    string? GetCachedAvailableUpdate();

    /// <summary>
    /// Checks if a background update check should be started.
    /// </summary>
    bool ShouldStartBackgroundCheck();

    /// <summary>
    /// Starts a background update check process.
    /// </summary>
    void StartBackgroundCheck();

    /// <summary>
    /// Checks if update checking is disabled via environment variable.
    /// </summary>
    bool IsUpdateCheckDisabled();
}

/// <summary>
/// Result of an update check.
/// </summary>
public record UpdateCheckResult(
    bool Success,
    string? LatestVersion,
    string? CurrentVersion,
    bool UpdateAvailable);

/// <summary>
/// Service for checking if updates are available.
/// </summary>
public class UpdateChecker : IUpdateChecker
{
    private const string PackageId = "dotnet-dev-certs-plus";
    private const string GitHubOwner = "DamianEdwards";
    private const string DisableEnvVar = "DOTNET_DEV_CERTS_PLUS_DISABLE_UPDATE_CHECK";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    private readonly IUpdateStateManager _stateManager;
    private readonly INuGetClient _nugetClient;
    private readonly IGitHubPackagesClient _githubClient;

    /// <summary>
    /// Creates a new UpdateChecker with default dependencies.
    /// </summary>
    public UpdateChecker() : this(new UpdateStateManager(), new NuGetClient(), new GitHubPackagesClient())
    {
    }

    /// <summary>
    /// Creates a new UpdateChecker with custom dependencies (for testing).
    /// </summary>
    public UpdateChecker(IUpdateStateManager stateManager, INuGetClient nugetClient, IGitHubPackagesClient githubClient)
    {
        _stateManager = stateManager;
        _nugetClient = nugetClient;
        _githubClient = githubClient;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = VersionInfo.GetCurrentVersion();
        var currentBuildType = VersionInfo.GetBuildType(currentVersion);

        try
        {
            // Always update the last check time
            _stateManager.UpdateLastCheckTime();

            var allVersions = new List<string>();

            // Get versions from NuGet.org
            var nugetVersions = await _nugetClient.GetPackageVersionsAsync(PackageId, cancellationToken);
            allVersions.AddRange(nugetVersions);

            // For dev builds, also check GitHub Packages
            if (currentBuildType == BuildType.Dev)
            {
                var githubVersions = await _githubClient.GetPackageVersionsAsync(GitHubOwner, PackageId, cancellationToken);
                allVersions.AddRange(githubVersions);
            }

            // Find the latest version that qualifies as an update
            string? latestUpdate = null;

            foreach (var version in allVersions)
            {
                if (VersionInfo.IsUpdateAvailable(currentVersion, version, currentBuildType))
                {
                    if (latestUpdate is null || VersionInfo.CompareVersions(latestUpdate, version) < 0)
                    {
                        latestUpdate = version;
                    }
                }
            }

            // Update state
            if (latestUpdate is not null)
            {
                _stateManager.SetAvailableUpdate(latestUpdate);
            }
            else
            {
                _stateManager.ClearAvailableUpdate();
            }

            return new UpdateCheckResult(
                Success: true,
                LatestVersion: latestUpdate,
                CurrentVersion: currentVersion,
                UpdateAvailable: latestUpdate is not null);
        }
        catch
        {
            // Don't clear existing state on error - keep showing old update if available
            return new UpdateCheckResult(
                Success: false,
                LatestVersion: null,
                CurrentVersion: currentVersion,
                UpdateAvailable: false);
        }
    }

    /// <inheritdoc/>
    public string? GetCachedAvailableUpdate()
    {
        return _stateManager.GetAvailableUpdate();
    }

    /// <inheritdoc/>
    public bool ShouldStartBackgroundCheck()
    {
        if (IsUpdateCheckDisabled())
        {
            return false;
        }

        return _stateManager.ShouldCheckForUpdate(CheckInterval);
    }

    /// <inheritdoc/>
    public void StartBackgroundCheck()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath))
            {
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = "https --check-update",
                UseShellExecute = false,
                CreateNoWindow = true
                // Don't redirect output - we won't read it and it could cause hangs
            };

            var process = Process.Start(startInfo);
            process?.Dispose(); // Dispose handle immediately - process continues running
        }
        catch
        {
            // Ignore errors starting background process
        }
    }

    /// <inheritdoc/>
    public bool IsUpdateCheckDisabled()
    {
        var value = Environment.GetEnvironmentVariable(DisableEnvVar);
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
