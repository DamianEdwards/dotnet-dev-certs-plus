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
    private readonly IUpdateLogger _logger;

    /// <summary>
    /// Creates a new UpdateChecker with default dependencies.
    /// </summary>
    public UpdateChecker() : this(new UpdateStateManager(), new NuGetClient(), new GitHubPackagesClient(), new UpdateLogger())
    {
    }

    /// <summary>
    /// Creates a new UpdateChecker with custom dependencies (for testing).
    /// </summary>
    public UpdateChecker(IUpdateStateManager stateManager, INuGetClient nugetClient, IGitHubPackagesClient githubClient, IUpdateLogger? logger = null)
    {
        _stateManager = stateManager;
        _nugetClient = nugetClient;
        _githubClient = githubClient;
        _logger = logger ?? NullUpdateLogger.Instance;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = VersionInfo.GetCurrentVersion();
        var currentBuildType = VersionInfo.GetBuildType(currentVersion);

        _logger.Log("UpdateCheck", $"Started - Current version: {currentVersion}, Build type: {currentBuildType}");

        try
        {
            // Always update the last check time
            _stateManager.UpdateLastCheckTime();
            _logger.Log("UpdateCheck", "Updated last check time");

            var allVersions = new List<string>();

            // Get versions from NuGet.org
            _logger.Log("NuGet", $"Querying package: {PackageId}");
            var nugetVersions = await _nugetClient.GetPackageVersionsAsync(PackageId, cancellationToken);
            allVersions.AddRange(nugetVersions);
            _logger.Log("NuGet", $"Found {nugetVersions.Count} versions: {string.Join(", ", nugetVersions.Take(10))}{(nugetVersions.Count > 10 ? "..." : "")}");

            // For dev builds, also check GitHub Packages
            if (currentBuildType == BuildType.Dev)
            {
                _logger.Log("GitHub", $"Querying package (dev build): {GitHubOwner}/{PackageId}");
                var githubVersions = await _githubClient.GetPackageVersionsAsync(GitHubOwner, PackageId, cancellationToken);
                allVersions.AddRange(githubVersions);
                _logger.Log("GitHub", $"Found {githubVersions.Count} versions");
            }

            // Find the latest version that qualifies as an update
            string? latestUpdate = null;
            _logger.Log("VersionCheck", $"Checking {allVersions.Count} versions for updates");

            foreach (var version in allVersions)
            {
                var isUpdate = VersionInfo.IsUpdateAvailable(currentVersion, version, currentBuildType);
                var comparison = VersionInfo.CompareVersions(currentVersion, version);
                _logger.Log("VersionCheck", $"  {version}: compare={comparison}, isUpdate={isUpdate}");
                
                if (isUpdate)
                {
                    if (latestUpdate is null || VersionInfo.CompareVersions(latestUpdate, version) < 0)
                    {
                        _logger.Log("VersionCheck", $"New candidate update: {version} (previous: {latestUpdate ?? "none"})");
                        latestUpdate = version;
                    }
                }
            }

            // Update state
            if (latestUpdate is not null)
            {
                _stateManager.SetAvailableUpdate(latestUpdate);
                _logger.Log("UpdateCheck", $"Update available: {latestUpdate} - wrote to state file");
            }
            else
            {
                _stateManager.ClearAvailableUpdate();
                _logger.Log("UpdateCheck", "No update available - cleared state file");
            }

            _logger.Log("UpdateCheck", $"Completed successfully - UpdateAvailable: {latestUpdate is not null}");

            return new UpdateCheckResult(
                Success: true,
                LatestVersion: latestUpdate,
                CurrentVersion: currentVersion,
                UpdateAvailable: latestUpdate is not null);
        }
        catch (Exception ex)
        {
            _logger.Log("UpdateCheck", $"Failed with exception: {ex.GetType().Name}: {ex.Message}");
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
