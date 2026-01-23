using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using DotnetDevCertsPlus.Commands;
using DotnetDevCertsPlus.Services;
using NSubstitute;

namespace DotnetDevCertsPlus.Tests.Integration;

public class CommandLineParsingTests
{
    [Fact]
    public void HttpsCommand_HasCorrectName()
    {
        // Act
        var command = HttpsCommand.Create();

        // Assert
        Assert.Equal("https", command.Name);
    }

    [Fact]
    public void HttpsCommand_HasAllExpectedOptions()
    {
        // Act
        var command = HttpsCommand.Create();
        var optionNames = command.Options.Select(o => o.Name).ToList();

        // Assert - Extended options (names include -- prefix)
        Assert.Contains("--store", optionNames);
        Assert.Contains("--wsl", optionNames);
        Assert.Contains("--force", optionNames);
        Assert.Contains("--check-update", optionNames);

        // Assert - Standard dotnet dev-certs options
        Assert.Contains("--export-path", optionNames);
        Assert.Contains("--password", optionNames);
        Assert.Contains("--no-password", optionNames);
        Assert.Contains("--check", optionNames);
        Assert.Contains("--clean", optionNames);
        Assert.Contains("--import", optionNames);
        Assert.Contains("--format", optionNames);
        Assert.Contains("--trust", optionNames);
        Assert.Contains("--verbose", optionNames);
        Assert.Contains("--quiet", optionNames);
    }

    [Theory]
    [InlineData("https --store machine")]
    [InlineData("https --store")]
    [InlineData("https --check")]
    [InlineData("https --trust")]
    [InlineData("https --check --trust")]
    [InlineData("https --clean")]
    [InlineData("https --clean --force")]
    [InlineData("https --verbose")]
    [InlineData("https --quiet")]
    [InlineData("https --check-update")]
    public void Parse_ValidCommandLines_Succeeds(string commandLine)
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse(commandLine);

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Theory]
    [InlineData("https --wsl")]
    [InlineData("https --wsl Ubuntu")]
    public void Parse_WslCommandLines_SucceedsOnWindows(string commandLine)
    {
        // WSL is only supported on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse(commandLine);

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Theory]
    [InlineData("https --wsl")]
    [InlineData("https --wsl Ubuntu")]
    public void Parse_WslCommandLines_ReturnsErrorOnNonWindows(string commandLine)
    {
        // This test only runs on non-Windows platforms
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse(commandLine);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, e => e.Message.Contains("only supported on Windows", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https --store invalid", "The only supported store value is 'machine'")]
    [InlineData("https --clean --check", "cannot be combined")]
    [InlineData("https --password test --no-password", "cannot be combined")]
    [InlineData("https --format invalid", "must be 'Pfx' or 'Pem'")]
    [InlineData("https --check-update --check", "cannot be combined")]
    [InlineData("https --check-update --clean", "cannot be combined")]
    [InlineData("https --check-update --store machine", "cannot be combined")]
    [InlineData("https --check-update --trust", "cannot be combined")]
    public void Parse_InvalidCommandLines_ReturnsErrors(string commandLine, string expectedErrorSubstring)
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse(commandLine);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, e => e.Message.Contains(expectedErrorSubstring, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_StoreAndWslCombined_ReturnsError()
    {
        // This test verifies that --store and --wsl cannot be combined
        // On non-Windows, the --wsl error will trigger instead
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse("https --store machine --wsl");

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, e => e.Message.Contains("cannot be combined", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https --store machine --import /path/cert.pfx", "cannot be combined")]
    [InlineData("https --store machine --export-path /path/cert.pfx", "cannot be combined")]
    [InlineData("https --store machine --format Pfx", "cannot be combined")]
    [InlineData("https --store machine --password test", "cannot be combined")]
    [InlineData("https --store machine --no-password", "cannot be combined")]
    public void Parse_StoreWithPassthroughOptions_ReturnsErrors(string commandLine, string expectedErrorSubstring)
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse(commandLine);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, e => e.Message.Contains(expectedErrorSubstring, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https --wsl --import /path/cert.pfx", "cannot be combined")]
    [InlineData("https --wsl --export-path /path/cert.pfx", "cannot be combined")]
    public void Parse_WslWithPassthroughOptions_ReturnsErrors(string commandLine, string expectedErrorSubstring)
    {
        // WSL is only supported on Windows, so this test only runs on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse(commandLine);

        // Assert
        Assert.NotEmpty(parseResult.Errors);
        Assert.Contains(parseResult.Errors, e => e.Message.Contains(expectedErrorSubstring, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("https --format Pfx")]
    [InlineData("https --format Pem")]
    [InlineData("https --format pfx")]
    [InlineData("https --format pem")]
    public void Parse_ValidFormats_Succeeds(string commandLine)
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse(commandLine);

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void Parse_ExportPathWithAlias_Works()
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse("https -ep /path/to/cert.pfx");

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void Parse_PasswordWithAlias_Works()
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse("https -p mypassword");

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void Parse_TrustWithAlias_Works()
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse("https -t");

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void Parse_CheckWithAlias_Works()
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse("https -c");

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void Parse_VerboseWithAlias_Works()
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse("https -v");

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void Parse_QuietWithAlias_Works()
    {
        // Arrange
        var command = HttpsCommand.Create();
        var rootCommand = new RootCommand { command };

        // Act
        var parseResult = rootCommand.Parse("https -q");

        // Assert
        Assert.Empty(parseResult.Errors);
    }

    [Fact]
    public void Create_WithCustomServiceFactory_UsesProvidedFactory()
    {
        // Arrange
        var mockFactory = Substitute.For<ServiceFactory>();
        var mockProcessRunner = Substitute.For<IProcessRunner>();
        mockFactory.CreateProcessRunner().Returns(mockProcessRunner);

        // Act
        var command = HttpsCommand.Create(mockFactory);

        // Assert
        Assert.NotNull(command);
    }
}
