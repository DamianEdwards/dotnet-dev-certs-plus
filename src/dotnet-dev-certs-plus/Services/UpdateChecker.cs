using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly ILogger<UpdateChecker> _logger;

    /// <summary>
    /// Creates a new UpdateChecker with default dependencies.
    /// </summary>
    public UpdateChecker(ILoggerFactory loggerFactory) 
        : this(new UpdateStateManager(), new NuGetClient(loggerFactory), new GitHubPackagesClient(), loggerFactory.CreateLogger<UpdateChecker>())
    {
    }

    /// <summary>
    /// Creates a new UpdateChecker with custom dependencies (for testing).
    /// </summary>
    public UpdateChecker(IUpdateStateManager stateManager, INuGetClient nugetClient, IGitHubPackagesClient githubClient, ILogger<UpdateChecker>? logger = null)
    {
        _stateManager = stateManager;
        _nugetClient = nugetClient;
        _githubClient = githubClient;
        _logger = logger ?? NullLogger<UpdateChecker>.Instance;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = VersionInfo.GetCurrentVersion();
        var currentBuildType = VersionInfo.GetBuildType(currentVersion);

        _logger.LogDebug("Update check started - Current version: {CurrentVersion}, Build type: {BuildType}", currentVersion, currentBuildType);

        try
        {
            // Always update the last check time
            _stateManager.UpdateLastCheckTime();
            _logger.LogDebug("Updated last check time");

            var allVersions = new List<string>();

            // Get versions from NuGet.org
            _logger.LogDebug("Querying NuGet for package: {PackageId}", PackageId);
            var nugetVersions = await _nugetClient.GetPackageVersionsAsync(PackageId, cancellationToken);
            allVersions.AddRange(nugetVersions);
            _logger.LogDebug("Found {Count} versions from NuGet: {Versions}", nugetVersions.Count, 
                string.Join(", ", nugetVersions.Take(10)) + (nugetVersions.Count > 10 ? "..." : ""));

            // For dev builds, also check GitHub Packages
            if (currentBuildType == BuildType.Dev)
            {
                _logger.LogDebug("Querying GitHub Packages (dev build): {Owner}/{PackageId}", GitHubOwner, PackageId);
                var githubVersions = await _githubClient.GetPackageVersionsAsync(GitHubOwner, PackageId, cancellationToken);
                allVersions.AddRange(githubVersions);
                _logger.LogDebug("Found {Count} versions from GitHub", githubVersions.Count);
            }

            // Find the latest version that qualifies as an update
            string? latestUpdate = null;
            _logger.LogDebug("Checking {Count} versions for updates", allVersions.Count);

            foreach (var version in allVersions)
            {
                var isUpdate = VersionInfo.IsUpdateAvailable(currentVersion, version, currentBuildType);
                var comparison = VersionInfo.CompareVersions(currentVersion, version);
                _logger.LogTrace("Version {Version}: compare={Comparison}, isUpdate={IsUpdate}", version, comparison, isUpdate);
                
                if (isUpdate)
                {
                    if (latestUpdate is null || VersionInfo.CompareVersions(latestUpdate, version) < 0)
                    {
                        _logger.LogDebug("New candidate update: {Version} (previous: {Previous})", version, latestUpdate ?? "none");
                        latestUpdate = version;
                    }
                }
            }

            // Update state
            if (latestUpdate is not null)
            {
                _stateManager.SetAvailableUpdate(latestUpdate);
                _logger.LogInformation("Update available: {LatestVersion} - wrote to state file", latestUpdate);
            }
            else
            {
                _stateManager.ClearAvailableUpdate();
                _logger.LogDebug("No update available - cleared state file");
            }

            _logger.LogDebug("Update check completed successfully - UpdateAvailable: {UpdateAvailable}", latestUpdate is not null);

            return new UpdateCheckResult(
                Success: true,
                LatestVersion: latestUpdate,
                CurrentVersion: currentVersion,
                UpdateAvailable: latestUpdate is not null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed");
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
