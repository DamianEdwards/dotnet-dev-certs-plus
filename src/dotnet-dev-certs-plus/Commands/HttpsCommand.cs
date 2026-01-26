using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using System.Text;
using DotnetDevCertsPlus.Models;
using DotnetDevCertsPlus.Services;

namespace DotnetDevCertsPlus.Commands;

public static class HttpsCommand
{
    // Exit codes matching dotnet dev-certs https behavior
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeError = 1;
    private const int ExitCodeCertificateNotFound = 2;
    private const int ExitCodeCertificateNotTrusted = 3;
    private const int ExitCodeDotnetNotAvailable = 4; // dev-certs-plus specific: dotnet CLI not available in WSL

    public static Command Create() => Create(ServiceFactory.Default);

    public static Command Create(ServiceFactory serviceFactory)
    {
        // Extended options (dev-certs-plus specific)
        var storeOption = new Option<string?>("--store")
        {
            Description = "Import cert to the specified store (machine)",
            Arity = ArgumentArity.ZeroOrOne
        };
        storeOption.Validators.Add(result =>
        {
            var value = result.GetValue(storeOption);
            if (value is not null && !value.Equals("machine", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("The only supported store value is 'machine'.");
            }
        });

        var wslOption = new Option<string?>("--wsl")
        {
            Description = "Import cert to WSL distro (default distro if no value specified)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Skip confirmation prompt when cleaning"
        };

        // Standard dotnet dev-certs https options
        var exportPathOption = new Option<string?>("--export-path")
        {
            Description = "Full path to the exported certificate"
        };
        exportPathOption.Aliases.Add("-ep");

        var passwordOption = new Option<string?>("--password")
        {
            Description = "Password to use when exporting the certificate with the private key into a pfx file or to encrypt the Pem exported key"
        };
        passwordOption.Aliases.Add("-p");

        var noPasswordOption = new Option<bool>("--no-password")
        {
            Description = "Explicitly request that you don't use a password for the key when exporting a certificate to a PEM format"
        };
        noPasswordOption.Aliases.Add("-np");

        var checkOption = new Option<bool>("--check")
        {
            Description = "Check for the existence of the certificate but do not perform any action"
        };
        checkOption.Aliases.Add("-c");

        var cleanOption = new Option<bool>("--clean")
        {
            Description = "Cleans all HTTPS development certificates from the machine"
        };

        var importOption = new Option<string?>("--import")
        {
            Description = "Imports the provided HTTPS development certificate into the machine. All other HTTPS developer certificates will be cleared out"
        };
        importOption.Aliases.Add("-i");

        var formatOption = new Option<string?>("--format")
        {
            Description = "Export the certificate in the given format. Valid values are Pfx and Pem. Pfx is the default."
        };
        formatOption.Validators.Add(result =>
        {
            var value = result.GetValue(formatOption);
            if (value is not null &&
                !value.Equals("Pfx", StringComparison.OrdinalIgnoreCase) &&
                !value.Equals("Pem", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("The format must be 'Pfx' or 'Pem'.");
            }
        });

        var trustOption = new Option<bool>("--trust")
        {
            Description = "Trust the certificate on the current platform"
        };
        trustOption.Aliases.Add("-t");

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Display more debug information"
        };
        verboseOption.Aliases.Add("-v");

        var quietOption = new Option<bool>("--quiet")
        {
            Description = "Display warnings and errors only"
        };
        quietOption.Aliases.Add("-q");

        var checkTrustMachineReadableOption = new Option<bool>("--check-trust-machine-readable")
        {
            Description = "Same as running --check --trust, but output the results in json"
        };

        var checkUpdateOption = new Option<bool>("--check-update")
        {
            Description = "Check if a new version of the tool is available"
        };

        var command = new Command("https", "Manage the HTTPS development certificate with extended functionality");

        // Add extended options
        command.Options.Add(storeOption);
        command.Options.Add(wslOption);
        command.Options.Add(forceOption);

        // Add standard options
        command.Options.Add(exportPathOption);
        command.Options.Add(passwordOption);
        command.Options.Add(noPasswordOption);
        command.Options.Add(checkOption);
        command.Options.Add(cleanOption);
        command.Options.Add(importOption);
        command.Options.Add(formatOption);
        command.Options.Add(trustOption);
        command.Options.Add(verboseOption);
        command.Options.Add(quietOption);
        command.Options.Add(checkTrustMachineReadableOption);
        command.Options.Add(checkUpdateOption);

        command.Validators.Add(result =>
        {
            var hasStore = result.GetResult(storeOption) is not null;
            var hasWsl = result.GetResult(wslOption) is not null;
            var hasClean = result.GetValue(cleanOption);
            var hasCheck = result.GetValue(checkOption);
            var hasPassword = result.GetResult(passwordOption) is not null;
            var hasNoPassword = result.GetValue(noPasswordOption);
            var hasImport = result.GetResult(importOption) is not null;
            var hasExportPath = result.GetResult(exportPathOption) is not null;
            var hasFormat = result.GetResult(formatOption) is not null;
            var hasCheckTrustMachineReadable = result.GetValue(checkTrustMachineReadableOption);
            var hasCheckUpdate = result.GetValue(checkUpdateOption);
            var hasTrust = result.GetValue(trustOption);

            // --check-update is a standalone operation
            if (hasCheckUpdate)
            {
                if (hasStore || hasWsl || hasClean || hasCheck || hasImport || hasExportPath || 
                    hasFormat || hasPassword || hasNoPassword || hasCheckTrustMachineReadable || hasTrust)
                {
                    result.AddError("Option '--check-update' cannot be combined with other options.");
                }
            }

            // --store and --wsl cannot be combined
            if (hasStore && hasWsl)
            {
                result.AddError("Options '--store' and '--wsl' cannot be combined.");
            }

            // --clean and --check are mutually exclusive
            if (hasClean && hasCheck)
            {
                result.AddError("Options '--clean' and '--check' cannot be combined.");
            }

            // --clean and --check-trust-machine-readable are mutually exclusive
            if (hasClean && hasCheckTrustMachineReadable)
            {
                result.AddError("Options '--clean' and '--check-trust-machine-readable' cannot be combined.");
            }

            // --password and --no-password are mutually exclusive
            if (hasPassword && hasNoPassword)
            {
                result.AddError("Options '--password' and '--no-password' cannot be combined.");
            }

            // WSL is Windows-only
            if (hasWsl && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result.AddError("Option '--wsl' is only supported on Windows.");
            }

            // When using --store or --wsl, certain passthrough-only options are not supported
            if (hasStore || hasWsl)
            {
                if (hasImport)
                {
                    result.AddError("Option '--import' cannot be combined with '--store' or '--wsl'.");
                }
                if (hasExportPath)
                {
                    result.AddError("Option '--export-path' cannot be combined with '--store' or '--wsl'.");
                }
                if (hasFormat)
                {
                    result.AddError("Option '--format' cannot be combined with '--store' or '--wsl'.");
                }
                if (hasPassword)
                {
                    result.AddError("Option '--password' cannot be combined with '--store' or '--wsl'.");
                }
                if (hasNoPassword)
                {
                    result.AddError("Option '--no-password' cannot be combined with '--store' or '--wsl'.");
                }
            }
        });

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var hasStore = parseResult.GetResult(storeOption) is not null;
            var hasWsl = parseResult.GetResult(wslOption) is not null;
            var checkUpdate = parseResult.GetValue(checkUpdateOption);

            var wslDistro = parseResult.GetValue(wslOption);
            var trust = parseResult.GetValue(trustOption);
            var check = parseResult.GetValue(checkOption);
            var clean = parseResult.GetValue(cleanOption);
            var force = parseResult.GetValue(forceOption);
            var verbose = parseResult.GetValue(verboseOption);
            var quiet = parseResult.GetValue(quietOption);
            var checkTrustMachineReadable = parseResult.GetValue(checkTrustMachineReadableOption);

            var output = new OutputHelper(verbose, quiet);
            var updateChecker = serviceFactory.CreateUpdateChecker();
            var versionProvider = serviceFactory.CreateVersionInfoProvider();

            // Handle --check-update flag (used by background process)
            if (checkUpdate)
            {
                return await HandleCheckUpdateAsync(updateChecker, versionProvider, output, cancellationToken);
            }

            // Start background update check if needed (fire and forget)
            if (updateChecker.ShouldStartBackgroundCheck())
            {
                updateChecker.StartBackgroundCheck();
            }

            int exitCode;
            if (hasStore)
            {
                var storeService = serviceFactory.CreateMachineStoreService();
                var certService = serviceFactory.CreateDevCertService();
                if (clean)
                {
                    exitCode = await HandleMachineStoreCleanAsync(storeService, force, output, cancellationToken);
                }
                else if (checkTrustMachineReadable)
                {
                    exitCode = await HandleMachineStoreCheckJsonAsync(storeService, cancellationToken);
                }
                else
                {
                    exitCode = await HandleMachineStoreAsync(certService, storeService, trust, check, output, cancellationToken);
                }
            }
            else if (hasWsl)
            {
                var wslService = serviceFactory.CreateWslService();
                var certService = serviceFactory.CreateDevCertService();
                if (clean)
                {
                    exitCode = await HandleWslCleanAsync(wslService, wslDistro, force, output, cancellationToken);
                }
                else if (checkTrustMachineReadable)
                {
                    exitCode = await HandleWslCheckJsonAsync(wslService, wslDistro, output, cancellationToken);
                }
                else
                {
                    exitCode = await HandleWslAsync(certService, wslService, wslDistro, trust, check, output, cancellationToken);
                }
            }
            else
            {
                // Passthrough mode - forward to dotnet dev-certs https
                var processRunner = serviceFactory.CreateProcessRunner();
                exitCode = await HandlePassthroughAsync(processRunner, parseResult, exportPathOption, passwordOption, noPasswordOption,
                    checkOption, cleanOption, importOption, formatOption, trustOption, verboseOption, quietOption,
                    checkTrustMachineReadableOption, cancellationToken);
            }

            // Check for and display update notification (unless quiet mode or JSON output)
            if (!quiet && !checkTrustMachineReadable)
            {
                DisplayUpdateNotification(updateChecker, versionProvider, output);
            }

            return exitCode;
        });

        return command;
    }

    private static async Task<int> HandleCheckUpdateAsync(IUpdateChecker updateChecker, IVersionInfoProvider versionProvider, OutputHelper output, CancellationToken cancellationToken)
    {
        output.WriteLine($"Checking for updates (current version: {versionProvider.GetCurrentVersion()})...");
        
        var result = await updateChecker.CheckForUpdateAsync(cancellationToken);
        
        if (!result.Success)
        {
            output.WriteError("Failed to check for updates.");
            return ExitCodeError;
        }

        if (result.UpdateAvailable)
        {
            output.WriteLine($"A new version is available: {result.LatestVersion}");
            DisplayUpdateInstructions(result.LatestVersion!, output);
        }
        else
        {
            output.WriteLine("You are running the latest version.");
        }

        return ExitCodeSuccess;
    }

    private static void DisplayUpdateNotification(IUpdateChecker updateChecker, IVersionInfoProvider versionProvider, OutputHelper output)
    {
        var availableVersion = updateChecker.GetCachedAvailableUpdate();
        if (string.IsNullOrEmpty(availableVersion))
        {
            return;
        }

        // Verify it's still a valid update (in case user updated but state file wasn't cleared)
        var currentVersion = versionProvider.GetCurrentVersion();
        var currentBuildType = versionProvider.GetCurrentBuildType();
        if (!VersionInfo.IsUpdateAvailable(currentVersion, availableVersion, currentBuildType))
        {
            return;
        }

        Console.Error.WriteLine();
        try
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Error.WriteLine($"⚠  A new version of dotnet-dev-certs-plus is available: {availableVersion}");
        }
        finally
        {
            Console.ResetColor();
        }
        DisplayUpdateInstructions(availableVersion, output);
    }

    private static void DisplayUpdateInstructions(string targetVersion, OutputHelper output)
    {
        var command = GetUpdateCommand(targetVersion);
        output.WriteError($"   Update with: {command}");
    }

    /// <summary>
    /// Gets the appropriate update command based on the target version's build type.
    /// </summary>
    internal static string GetUpdateCommand(string targetVersion)
    {
        var buildType = VersionInfo.GetBuildType(targetVersion);
        return GetUpdateCommand(buildType);
    }

    /// <summary>
    /// Gets the appropriate update command for the specified build type.
    /// </summary>
    internal static string GetUpdateCommand(BuildType buildType)
    {
        return buildType switch
        {
            BuildType.Dev => "dotnet tool update -g dotnet-dev-certs-plus --prerelease --add-source https://nuget.pkg.github.com/DamianEdwards/index.json",
            BuildType.PreRelease => "dotnet tool update -g dotnet-dev-certs-plus --prerelease",
            _ => "dotnet tool update -g dotnet-dev-certs-plus"
        };
    }

    private static async Task<int> HandlePassthroughAsync(
        IProcessRunner processRunner,
        ParseResult parseResult,
        Option<string?> exportPathOption,
        Option<string?> passwordOption,
        Option<bool> noPasswordOption,
        Option<bool> checkOption,
        Option<bool> cleanOption,
        Option<string?> importOption,
        Option<string?> formatOption,
        Option<bool> trustOption,
        Option<bool> verboseOption,
        Option<bool> quietOption,
        Option<bool> checkTrustMachineReadableOption,
        CancellationToken cancellationToken)
    {
        var args = new StringBuilder("dev-certs https");

        var exportPath = parseResult.GetValue(exportPathOption);
        if (!string.IsNullOrEmpty(exportPath))
        {
            args.Append($" --export-path {ProcessRunner.EscapeArgument(exportPath)}");
        }

        var password = parseResult.GetValue(passwordOption);
        if (!string.IsNullOrEmpty(password))
        {
            args.Append($" --password {ProcessRunner.EscapeArgument(password)}");
        }

        if (parseResult.GetValue(noPasswordOption))
        {
            args.Append(" --no-password");
        }

        if (parseResult.GetValue(checkOption))
        {
            args.Append(" --check");
        }

        if (parseResult.GetValue(cleanOption))
        {
            args.Append(" --clean");
        }

        var importPath = parseResult.GetValue(importOption);
        if (!string.IsNullOrEmpty(importPath))
        {
            args.Append($" --import {ProcessRunner.EscapeArgument(importPath)}");
        }

        var format = parseResult.GetValue(formatOption);
        if (!string.IsNullOrEmpty(format))
        {
            args.Append($" --format {format}");
        }

        if (parseResult.GetValue(trustOption))
        {
            args.Append(" --trust");
        }

        if (parseResult.GetValue(verboseOption))
        {
            args.Append(" --verbose");
        }

        if (parseResult.GetValue(quietOption))
        {
            args.Append(" --quiet");
        }

        if (parseResult.GetValue(checkTrustMachineReadableOption))
        {
            args.Append(" --check-trust-machine-readable");
        }

        var result = await processRunner.RunAsync("dotnet", args.ToString(), cancellationToken);

        // Output the result directly (preserving original output)
        if (!string.IsNullOrEmpty(result.StandardOutput))
        {
            Console.Write(result.StandardOutput);
        }
        if (!string.IsNullOrEmpty(result.StandardError))
        {
            Console.Error.Write(result.StandardError);
        }

        return result.ExitCode;
    }

    private static async Task<int> HandleMachineStoreCheckJsonAsync(IMachineStoreService storeService, CancellationToken cancellationToken)
    {
        var certInfo = await storeService.GetCertificateInfoAsync(cancellationToken);

        if (certInfo is null)
        {
            Console.WriteLine("[]");
            return ExitCodeCertificateNotFound;
        }

        var json = CertificateInfo.ToJson([certInfo]);
        Console.WriteLine(json);

        return certInfo.TrustLevel == "Full" ? ExitCodeSuccess : ExitCodeCertificateNotTrusted;
    }

    private static async Task<int> HandleWslCheckJsonAsync(IWslService wslService, string? distro, OutputHelper output, CancellationToken cancellationToken)
    {
        // Determine which distro to use
        if (string.IsNullOrEmpty(distro))
        {
            distro = await wslService.GetDefaultDistroAsync(cancellationToken);
            if (string.IsNullOrEmpty(distro))
            {
                Console.Error.WriteLine("No default WSL distribution found.");
                return ExitCodeError;
            }
        }

        // Check if dotnet is available in WSL
        if (!await wslService.CheckDotnetAvailableAsync(distro, cancellationToken))
        {
            Console.Error.WriteLine($"dotnet CLI is not available in WSL distribution '{distro}'.");
            return ExitCodeDotnetNotAvailable;
        }

        var (json, exitCode) = await wslService.GetCertificateInfoJsonAsync(distro, cancellationToken);

        if (!string.IsNullOrEmpty(json))
        {
            Console.WriteLine(json);
        }

        return exitCode;
    }

    private static async Task<int> HandleMachineStoreAsync(IDevCertService certService, IMachineStoreService storeService, bool trust, bool check, OutputHelper output, CancellationToken cancellationToken)
    {
        if (check)
        {
            return await HandleMachineStoreCheckAsync(trust, storeService, output, cancellationToken);
        }

        // Ensure cert exists on the host
        output.WriteLine("Checking host dev certificate status...");
        var status = await certService.CheckStatusAsync(cancellationToken);

        if (!status.Exists)
        {
            output.WriteLine("Dev certificate not found. Creating...");
            if (!await certService.EnsureCreatedAsync(cancellationToken))
            {
                output.WriteError("Failed to create dev certificate.");
                return 1;
            }
            output.WriteLine("Dev certificate created.");
        }
        else
        {
            output.WriteLine("Dev certificate exists.");
        }

        // Export cert to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
        var password = Guid.NewGuid().ToString("N");

        try
        {
            output.WriteVerbose($"Exporting certificate to {tempPath}...");
            output.WriteLine("Exporting certificate...");
            if (!await certService.ExportAsync(tempPath, CertificateFormat.Pfx, password, cancellationToken))
            {
                output.WriteError("Failed to export dev certificate.");
                return 1;
            }

            // Import to machine store
            output.WriteLine("Importing to machine store...");
            if (!await storeService.ImportToMachineStoreAsync(tempPath, password, cancellationToken))
            {
                output.WriteError("Failed to import certificate to machine store. Ensure you have appropriate permissions (run as Administrator on Windows, use sudo on Linux/macOS).");
                return 1;
            }
            output.WriteLine("Certificate imported to machine store.");

            // Trust if requested
            if (trust)
            {
                output.WriteLine("Trusting certificate in machine store...");
                if (!await storeService.TrustInMachineStoreAsync(tempPath, password, cancellationToken))
                {
                    output.WriteError("Failed to trust certificate in machine store. Ensure you have appropriate permissions.");
                    return 1;
                }
                output.WriteLine("Certificate trusted in machine store.");
            }

            output.WriteLine("Done.");
            return 0;
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempPath))
            {
                output.WriteVerbose($"Cleaning up temporary file {tempPath}...");
                File.Delete(tempPath);
            }
        }
    }

    private static async Task<int> HandleMachineStoreCheckAsync(bool trust, IMachineStoreService storeService, OutputHelper output, CancellationToken cancellationToken)
    {
        output.WriteLine("Checking machine store status...");

        var exists = await storeService.CheckMachineStoreAsync(cancellationToken);

        if (!exists)
        {
            output.WriteLine("Certificate not found in machine store.");
            return ExitCodeCertificateNotFound;
        }

        output.WriteLine("Certificate exists in machine store.");

        if (trust)
        {
            var isTrusted = await storeService.CheckMachineTrustAsync(cancellationToken);
            if (!isTrusted)
            {
                output.WriteLine("Certificate not trusted in machine store.");
                return ExitCodeCertificateNotTrusted;
            }
            output.WriteLine("Certificate is trusted in machine store.");
        }

        return ExitCodeSuccess;
    }

    private static async Task<int> HandleMachineStoreCleanAsync(IMachineStoreService storeService, bool force, OutputHelper output, CancellationToken cancellationToken)
    {
        if (!force)
        {
            Console.Write("Are you sure you want to remove the HTTPS development certificate from the machine store? (y/N) ");
            var response = Console.ReadLine();
            if (response is null || !string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("Operation cancelled.");
                return 0;
            }
        }

        output.WriteLine("Removing certificate from machine store...");
        if (!await storeService.CleanMachineStoreAsync(cancellationToken))
        {
            output.WriteError("Failed to remove certificate from machine store. Ensure you have appropriate permissions (run as Administrator on Windows, use sudo on Linux/macOS).");
            return 1;
        }

        output.WriteLine("Certificate removed from machine store.");
        return 0;
    }

    private static async Task<int> HandleWslAsync(IDevCertService certService, IWslService wslService, string? distro, bool trust, bool check, OutputHelper output, CancellationToken cancellationToken)
    {
        // Determine which distro to use
        if (string.IsNullOrEmpty(distro))
        {
            distro = await wslService.GetDefaultDistroAsync(cancellationToken);
            if (string.IsNullOrEmpty(distro))
            {
                output.WriteError("No default WSL distribution found.");
                return ExitCodeError;
            }
            output.WriteLine($"Using default WSL distribution: {distro}");
        }
        else
        {
            output.WriteLine($"Using WSL distribution: {distro}");
        }

        // Check if dotnet is available in WSL
        output.WriteVerbose($"Checking if dotnet is available in WSL distribution '{distro}'...");
        output.WriteLine("Checking if dotnet is available in WSL...");
        if (!await wslService.CheckDotnetAvailableAsync(distro, cancellationToken))
        {
            output.WriteError($"dotnet CLI is not available in WSL distribution '{distro}'.");
            return ExitCodeDotnetNotAvailable;
        }

        if (check)
        {
            return await HandleWslCheckAsync(distro, trust, wslService, output, cancellationToken);
        }

        // Ensure cert exists on the Windows host
        output.WriteLine("Checking host dev certificate status...");
        var status = await certService.CheckStatusAsync(cancellationToken);

        if (!status.Exists)
        {
            output.WriteLine("Dev certificate not found on host. Creating...");
            if (!await certService.EnsureCreatedAsync(cancellationToken))
            {
                output.WriteError("Failed to create dev certificate.");
                return 1;
            }
            output.WriteLine("Dev certificate created.");
        }
        else
        {
            output.WriteLine("Dev certificate exists on host.");
        }

        // Export cert to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
        var password = Guid.NewGuid().ToString("N");

        try
        {
            output.WriteVerbose($"Exporting certificate to {tempPath}...");
            output.WriteLine("Exporting certificate...");
            if (!await certService.ExportAsync(tempPath, CertificateFormat.Pfx, password, cancellationToken))
            {
                output.WriteError("Failed to export dev certificate.");
                return 1;
            }

            // Import into WSL
            output.WriteLine($"Importing certificate into WSL ({distro})...");
            if (!await wslService.ImportCertAsync(tempPath, password, trust, distro, cancellationToken))
            {
                output.WriteError("Failed to import certificate into WSL.");
                return 1;
            }

            if (trust)
            {
                output.WriteLine("Certificate imported and trusted in WSL.");
            }
            else
            {
                output.WriteLine("Certificate imported into WSL.");
            }

            output.WriteLine("Done.");
            return 0;
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempPath))
            {
                output.WriteVerbose($"Cleaning up temporary file {tempPath}...");
                File.Delete(tempPath);
            }
        }
    }

    private static async Task<int> HandleWslCheckAsync(string distro, bool trust, IWslService wslService, OutputHelper output, CancellationToken cancellationToken)
    {
        output.WriteLine($"Checking certificate status in WSL ({distro})...");

        var (exists, trusted) = await wslService.CheckCertStatusAsync(distro, cancellationToken);

        if (!exists)
        {
            output.WriteLine("Certificate not found in WSL.");
            return ExitCodeCertificateNotFound;
        }

        output.WriteLine("Certificate exists in WSL.");

        if (trust)
        {
            if (!trusted)
            {
                output.WriteLine("Certificate not trusted in WSL.");
                return ExitCodeCertificateNotTrusted;
            }
            output.WriteLine("Certificate is trusted in WSL.");
        }

        return ExitCodeSuccess;
    }

    private static async Task<int> HandleWslCleanAsync(IWslService wslService, string? distro, bool force, OutputHelper output, CancellationToken cancellationToken)
    {
        // Determine which distro to use
        if (string.IsNullOrEmpty(distro))
        {
            distro = await wslService.GetDefaultDistroAsync(cancellationToken);
            if (string.IsNullOrEmpty(distro))
            {
                output.WriteError("No default WSL distribution found.");
                return ExitCodeError;
            }
            output.WriteLine($"Using default WSL distribution: {distro}");
        }
        else
        {
            output.WriteLine($"Using WSL distribution: {distro}");
        }

        // Check if dotnet is available in WSL
        output.WriteVerbose($"Checking if dotnet is available in WSL distribution '{distro}'...");
        output.WriteLine("Checking if dotnet is available in WSL...");
        if (!await wslService.CheckDotnetAvailableAsync(distro, cancellationToken))
        {
            output.WriteError($"dotnet CLI is not available in WSL distribution '{distro}'.");
            return ExitCodeDotnetNotAvailable;
        }

        if (!force)
        {
            Console.Write($"Are you sure you want to remove the HTTPS development certificate from WSL ({distro})? (y/N) ");
            var response = Console.ReadLine();
            if (response is null || !string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("Operation cancelled.");
                return ExitCodeSuccess;
            }
        }

        output.WriteLine($"Removing certificate from WSL ({distro})...");
        if (!await wslService.CleanCertAsync(distro, cancellationToken))
        {
            output.WriteError("Failed to remove certificate from WSL.");
            return ExitCodeError;
        }

        output.WriteLine("Certificate removed from WSL.");
        return ExitCodeSuccess;
    }
}

/// <summary>
/// Helper class for managing console output based on verbose/quiet settings.
/// </summary>
internal class OutputHelper(bool verbose, bool quiet)
{
    public void WriteLine(string message)
    {
        if (!quiet)
        {
            Console.WriteLine(message);
        }
    }

    public void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }

    public void WriteVerbose(string message)
    {
        if (verbose)
        {
            Console.Error.WriteLine($"[DEBUG] {message}");
        }
    }
}
