using DotnetDevCertsPlus.Services;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class UpdateStateManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly UpdateStateManager _manager;

    public UpdateStateManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"dotnet-dev-certs-plus-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _manager = new UpdateStateManager(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
        GC.SuppressFinalize(this);
    }

    #region GetLastCheckTime Tests

    [Fact]
    public void GetLastCheckTime_NoFile_ReturnsNull()
    {
        var result = _manager.GetLastCheckTime();
        
        Assert.Null(result);
    }

    [Fact]
    public void GetLastCheckTime_WithFile_ReturnsLastWriteTime()
    {
        var filePath = Path.Combine(_testDirectory, "last-checked");
        File.WriteAllText(filePath, string.Empty);
        var expectedTime = File.GetLastWriteTimeUtc(filePath);

        var result = _manager.GetLastCheckTime();

        Assert.NotNull(result);
        Assert.Equal(expectedTime, result.Value, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region UpdateLastCheckTime Tests

    [Fact]
    public void UpdateLastCheckTime_CreatesFile()
    {
        _manager.UpdateLastCheckTime();

        var filePath = Path.Combine(_testDirectory, "last-checked");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void UpdateLastCheckTime_UpdatesExistingFile()
    {
        var filePath = Path.Combine(_testDirectory, "last-checked");
        File.WriteAllText(filePath, string.Empty);
        var originalTime = File.GetLastWriteTimeUtc(filePath);
        
        // Wait a bit to ensure time difference
        Thread.Sleep(50);
        
        _manager.UpdateLastCheckTime();
        
        var newTime = File.GetLastWriteTimeUtc(filePath);
        Assert.True(newTime >= originalTime);
    }

    #endregion

    #region GetAvailableUpdate Tests

    [Fact]
    public void GetAvailableUpdate_NoFile_ReturnsNull()
    {
        var result = _manager.GetAvailableUpdate();
        
        Assert.Null(result);
    }

    [Fact]
    public void GetAvailableUpdate_EmptyFile_ReturnsNull()
    {
        var filePath = Path.Combine(_testDirectory, "update-available");
        File.WriteAllText(filePath, string.Empty);

        var result = _manager.GetAvailableUpdate();

        Assert.Null(result);
    }

    [Fact]
    public void GetAvailableUpdate_WithVersion_ReturnsVersion()
    {
        var filePath = Path.Combine(_testDirectory, "update-available");
        File.WriteAllText(filePath, "1.2.3");

        var result = _manager.GetAvailableUpdate();

        Assert.Equal("1.2.3", result);
    }

    [Fact]
    public void GetAvailableUpdate_TrimsWhitespace()
    {
        var filePath = Path.Combine(_testDirectory, "update-available");
        File.WriteAllText(filePath, "  1.2.3  \n");

        var result = _manager.GetAvailableUpdate();

        Assert.Equal("1.2.3", result);
    }

    #endregion

    #region SetAvailableUpdate Tests

    [Fact]
    public void SetAvailableUpdate_CreatesFile()
    {
        _manager.SetAvailableUpdate("2.0.0");

        var filePath = Path.Combine(_testDirectory, "update-available");
        Assert.True(File.Exists(filePath));
        Assert.Equal("2.0.0", File.ReadAllText(filePath));
    }

    [Fact]
    public void SetAvailableUpdate_OverwritesExisting()
    {
        _manager.SetAvailableUpdate("1.0.0");
        _manager.SetAvailableUpdate("2.0.0");

        var filePath = Path.Combine(_testDirectory, "update-available");
        Assert.Equal("2.0.0", File.ReadAllText(filePath));
    }

    #endregion

    #region ClearAvailableUpdate Tests

    [Fact]
    public void ClearAvailableUpdate_DeletesFile()
    {
        var filePath = Path.Combine(_testDirectory, "update-available");
        File.WriteAllText(filePath, "1.0.0");

        _manager.ClearAvailableUpdate();

        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public void ClearAvailableUpdate_NoFile_DoesNotThrow()
    {
        var exception = Record.Exception(() => _manager.ClearAvailableUpdate());
        
        Assert.Null(exception);
    }

    #endregion

    #region ShouldCheckForUpdate Tests

    [Fact]
    public void ShouldCheckForUpdate_NoLastCheck_ReturnsTrue()
    {
        var result = _manager.ShouldCheckForUpdate(TimeSpan.FromMinutes(15));
        
        Assert.True(result);
    }

    [Fact]
    public void ShouldCheckForUpdate_RecentCheck_ReturnsFalse()
    {
        _manager.UpdateLastCheckTime();

        var result = _manager.ShouldCheckForUpdate(TimeSpan.FromMinutes(15));

        Assert.False(result);
    }

    [Fact]
    public void ShouldCheckForUpdate_OldCheck_ReturnsTrue()
    {
        var filePath = Path.Combine(_testDirectory, "last-checked");
        File.WriteAllText(filePath, string.Empty);
        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(-20));

        var result = _manager.ShouldCheckForUpdate(TimeSpan.FromMinutes(15));

        Assert.True(result);
    }

    #endregion
}
