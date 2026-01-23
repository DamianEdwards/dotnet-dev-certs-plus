namespace DotnetDevCertsPlus.Services;

/// <summary>
/// Interface for managing update check state files.
/// </summary>
public interface IUpdateStateManager
{
    /// <summary>
    /// Gets the time of the last update check.
    /// Returns null if no check has been performed.
    /// </summary>
    DateTime? GetLastCheckTime();

    /// <summary>
    /// Updates the last check time to now.
    /// </summary>
    void UpdateLastCheckTime();

    /// <summary>
    /// Gets the available update version, if any.
    /// Returns null if no update is available.
    /// </summary>
    string? GetAvailableUpdate();

    /// <summary>
    /// Writes the available update version to state.
    /// </summary>
    void SetAvailableUpdate(string version);

    /// <summary>
    /// Clears the available update state.
    /// </summary>
    void ClearAvailableUpdate();

    /// <summary>
    /// Checks if enough time has elapsed since the last check to warrant a new check.
    /// </summary>
    bool ShouldCheckForUpdate(TimeSpan checkInterval);
}

/// <summary>
/// Manages state files for update checking in ~/.dotnet-dev-certs-plus/
/// </summary>
public class UpdateStateManager : IUpdateStateManager
{
    private const string StateDirectoryName = ".dotnet-dev-certs-plus";
    private const string LastCheckedFileName = "last-checked";
    private const string UpdateAvailableFileName = "update-available";

    private readonly string _stateDirectory;
    private readonly string _lastCheckedPath;
    private readonly string _updateAvailablePath;

    /// <summary>
    /// Creates a new UpdateStateManager with the default state directory.
    /// </summary>
    public UpdateStateManager() : this(GetDefaultStateDirectory())
    {
    }

    /// <summary>
    /// Creates a new UpdateStateManager with a custom state directory (for testing).
    /// </summary>
    public UpdateStateManager(string stateDirectory)
    {
        _stateDirectory = stateDirectory;
        _lastCheckedPath = Path.Combine(_stateDirectory, LastCheckedFileName);
        _updateAvailablePath = Path.Combine(_stateDirectory, UpdateAvailableFileName);
    }

    /// <inheritdoc/>
    public DateTime? GetLastCheckTime()
    {
        try
        {
            if (File.Exists(_lastCheckedPath))
            {
                return File.GetLastWriteTimeUtc(_lastCheckedPath);
            }
        }
        catch
        {
            // Ignore file access errors
        }
        return null;
    }

    /// <inheritdoc/>
    public void UpdateLastCheckTime()
    {
        try
        {
            EnsureStateDirectoryExists();
            
            // Simply write/overwrite the file - avoids race condition between exists check and operation
            File.WriteAllText(_lastCheckedPath, string.Empty);
        }
        catch
        {
            // Ignore file access errors
        }
    }

    /// <inheritdoc/>
    public string? GetAvailableUpdate()
    {
        try
        {
            if (File.Exists(_updateAvailablePath))
            {
                var content = File.ReadAllText(_updateAvailablePath).Trim();
                return string.IsNullOrEmpty(content) ? null : content;
            }
        }
        catch
        {
            // Ignore file access errors
        }
        return null;
    }

    /// <inheritdoc/>
    public void SetAvailableUpdate(string version)
    {
        try
        {
            EnsureStateDirectoryExists();
            File.WriteAllText(_updateAvailablePath, version);
        }
        catch
        {
            // Ignore file access errors
        }
    }

    /// <inheritdoc/>
    public void ClearAvailableUpdate()
    {
        try
        {
            if (File.Exists(_updateAvailablePath))
            {
                File.Delete(_updateAvailablePath);
            }
        }
        catch
        {
            // Ignore file access errors
        }
    }

    /// <inheritdoc/>
    public bool ShouldCheckForUpdate(TimeSpan checkInterval)
    {
        var lastCheck = GetLastCheckTime();
        if (lastCheck is null)
        {
            return true;
        }

        return DateTime.UtcNow - lastCheck.Value > checkInterval;
    }

    private void EnsureStateDirectoryExists()
    {
        if (!Directory.Exists(_stateDirectory))
        {
            Directory.CreateDirectory(_stateDirectory);
        }
    }

    private static string GetDefaultStateDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, StateDirectoryName);
    }
}
