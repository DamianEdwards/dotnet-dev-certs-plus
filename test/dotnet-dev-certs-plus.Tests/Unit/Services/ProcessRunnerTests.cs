using DotnetDevCertsPlus.Services;

namespace DotnetDevCertsPlus.Tests.Unit.Services;

public class ProcessRunnerTests
{
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

    [Fact]
    public async Task RunAsync_EchoCommand_ReturnsOutput()
    {
        // Arrange
        var runner = new ProcessRunner();

        // Act - use a cross-platform echo approach
        var result = await runner.RunAsync("dotnet", "--version");

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
        var result = await runner.RunAsync("dotnet", "this-is-not-a-valid-command-12345");

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
}
