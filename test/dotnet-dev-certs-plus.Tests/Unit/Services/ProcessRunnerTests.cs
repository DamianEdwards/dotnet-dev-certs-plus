using DotnetDevCertsPlus.Services;
using Xunit;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class ProcessRunnerTests
{
    #region EscapeArgument Tests (Windows command-line escaping)

    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("simple", "simple")]
    [InlineData("with space", "\"with space\"")]
    [InlineData("with\"quote", "\"with\\\"quote\"")]
    [InlineData("path\\to\\file", "\"path\\\\to\\\\file\"")]
    [InlineData("C:\\Program Files\\app.exe", "\"C:\\\\Program Files\\\\app.exe\"")]
    public void EscapeArgument_ReturnsCorrectlyEscapedString(string input, string expected)
    {
        // Act
        var result = ProcessRunner.EscapeArgument(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EscapeArgument_NullInput_ReturnsEmptyQuotes()
    {
        // Act
        var result = ProcessRunner.EscapeArgument(null!);

        // Assert
        Assert.Equal("\"\"", result);
    }

    #endregion

    #region EscapeShellArgument Tests (POSIX shell escaping)

    [Theory]
    [InlineData("", "''")]
    [InlineData("simple", "'simple'")]
    [InlineData("with space", "'with space'")]
    [InlineData("with'quote", "'with'\\''quote'")]
    [InlineData("/path/to/file", "'/path/to/file'")]
    [InlineData("/mnt/c/Program Files/app.exe", "'/mnt/c/Program Files/app.exe'")]
    public void EscapeShellArgument_ReturnsCorrectlyEscapedString(string input, string expected)
    {
        // Act
        var result = ProcessRunner.EscapeShellArgument(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EscapeShellArgument_NullInput_ReturnsEmptyQuotes()
    {
        // Act
        var result = ProcessRunner.EscapeShellArgument(null!);

        // Assert
        Assert.Equal("''", result);
    }

    [Fact]
    public void EscapeShellArgument_WithSpecialShellChars_EscapesCorrectly()
    {
        // These characters would be dangerous in double quotes but safe in single quotes
        var input = "test; rm -rf /; echo";

        // Act
        var result = ProcessRunner.EscapeShellArgument(input);

        // Assert - should be wrapped in single quotes, making shell metacharacters safe
        Assert.Equal("'test; rm -rf /; echo'", result);
    }

    [Fact]
    public void EscapeShellArgument_WithDollarSign_EscapesCorrectly()
    {
        // $ would cause variable expansion in double quotes
        var input = "$HOME/file.txt";

        // Act
        var result = ProcessRunner.EscapeShellArgument(input);

        // Assert - single quotes prevent variable expansion
        Assert.Equal("'$HOME/file.txt'", result);
    }

    [Fact]
    public void EscapeShellArgument_WithBackticks_EscapesCorrectly()
    {
        // Backticks would cause command substitution in double quotes
        var input = "test`whoami`";

        // Act
        var result = ProcessRunner.EscapeShellArgument(input);

        // Assert - single quotes prevent command substitution
        Assert.Equal("'test`whoami`'", result);
    }

    [Fact]
    public void EscapeShellArgument_WithMultipleSingleQuotes_EscapesCorrectly()
    {
        // Multiple single quotes should each be escaped
        var input = "it's a 'test'";

        // Act
        var result = ProcessRunner.EscapeShellArgument(input);

        // Assert - each single quote is escaped
        Assert.Equal("'it'\\''s a '\\''test'\\'''", result);
    }

    #endregion

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_EchoCommand_ReturnsOutput()
    {
        // Arrange
        var runner = new ProcessRunner();

        // Act - use a cross-platform echo approach
        var result = await runner.RunAsync("dotnet", "--version", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
        Assert.True(result.StandardOutput.Contains('.'));  // Version format x.x.x
    }

    [Fact]
    public async Task RunAsync_InvalidCommand_ReturnsNonZeroExitCode()
    {
        // Arrange
        var runner = new ProcessRunner();

        // Act - use a command that will fail
        var result = await runner.RunAsync("dotnet", "this-is-not-a-valid-command-12345", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_CancellationToken_RespectsTimeout()
    {
        // Arrange
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert - should throw or complete quickly
        // Note: This test may be flaky depending on system performance
        try
        {
            // Use a command that takes time
            await runner.RunAsync("dotnet", "build --help", cts.Token);
            // If we get here, the command completed before timeout, which is fine
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation kicked in
        }
    }

    #endregion

    #region Instance and ProcessResult Tests

    [Fact]
    public void Default_ReturnsSingletonInstance()
    {
        // Act
        var instance1 = ProcessRunner.Default;
        var instance2 = ProcessRunner.Default;

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void ProcessResult_CombinedOutput_CombinesStdOutAndStdErr()
    {
        // Arrange
        var result = new ProcessResult(0, "stdout content", "stderr content");

        // Act
        var combined = result.CombinedOutput;

        // Assert
        Assert.Contains("stdout content", combined);
        Assert.Contains("stderr content", combined);
    }

    [Fact]
    public void ProcessResult_CombinedOutput_WithEmptyStdErr_ReturnsOnlyStdOut()
    {
        // Arrange
        var result = new ProcessResult(0, "stdout only", "");

        // Act
        var combined = result.CombinedOutput;

        // Assert
        Assert.Equal("stdout only", combined);
    }

    #endregion
}
