using System.CommandLine;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using DotnetDevCertsPlus.Services;

namespace DotnetDevCertsPlus.Commands;

public static class HttpsCommand
{
    public static Command Create()
    {
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

        var trustOption = new Option<bool>("--trust")
        {
            Description = "Trust the certificate"
        };

        var checkOption = new Option<bool>("--check")
        {
            Description = "Check certificate status (don't create/import)"
        };

        var command = new Command("https", "Manage the HTTPS development certificate with extended functionality");
        command.Options.Add(storeOption);
        command.Options.Add(wslOption);
        command.Options.Add(trustOption);
        command.Options.Add(checkOption);

        command.Validators.Add(result =>
        {
            // Check if both --store and --wsl are specified
            var hasStore = result.GetResult(storeOption) is not null;
            var hasWsl = result.GetResult(wslOption) is not null;

            if (hasStore && hasWsl)
            {
                result.AddError("Options '--store' and '--wsl' cannot be combined.");
            }

            // Require at least one of --store or --wsl
            if (!hasStore && !hasWsl)
            {
                result.AddError("At least one of '--store' or '--wsl' must be specified.");
            }

            // WSL is Windows-only
            if (hasWsl && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result.AddError("Option '--wsl' is only supported on Windows.");
            }
        });

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var storeValue = parseResult.GetValue(storeOption);
            var wslDistro = parseResult.GetValue(wslOption);
            var trust = parseResult.GetValue(trustOption);
            var check = parseResult.GetValue(checkOption);

            var hasStore = parseResult.GetResult(storeOption) is not null;
            var hasWsl = parseResult.GetResult(wslOption) is not null;

            if (hasStore)
            {
                return await HandleMachineStoreAsync(trust, check, cancellationToken);
            }
            else if (hasWsl)
            {
                return await HandleWslAsync(wslDistro, trust, check, cancellationToken);
            }

            return 0;
        });

        return command;
    }

    private static async Task<int> HandleMachineStoreAsync(bool trust, bool check, CancellationToken cancellationToken)
    {
        var certService = new DevCertService();
        var storeService = new WindowsCertStoreService();

        if (check)
        {
            return HandleMachineStoreCheck(trust, storeService);
        }

        // Ensure cert exists on the host
        Console.WriteLine("Checking host dev certificate status...");
        var status = await certService.CheckStatusAsync(cancellationToken);

        if (!status.Exists)
        {
            Console.WriteLine("Dev certificate not found. Creating...");
            if (!await certService.EnsureCreatedAsync(cancellationToken))
            {
                Console.Error.WriteLine("Failed to create dev certificate.");
                return 1;
            }
            Console.WriteLine("Dev certificate created.");
        }
        else
        {
            Console.WriteLine("Dev certificate exists.");
        }

        // Export cert to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
        var password = Guid.NewGuid().ToString("N");

        try
        {
            Console.WriteLine("Exporting certificate...");
            if (!await certService.ExportAsync(tempPath, CertificateFormat.Pfx, password, cancellationToken))
            {
                Console.Error.WriteLine("Failed to export dev certificate.");
                return 1;
            }

            // Import to machine store
            Console.WriteLine("Importing to LocalMachine\\My store...");
            if (!storeService.ImportToMachineStore(tempPath, password))
            {
                Console.Error.WriteLine("Failed to import certificate to machine store. Ensure you are running as Administrator.");
                return 1;
            }
            Console.WriteLine("Certificate imported to LocalMachine\\My store.");

            // Trust if requested
            if (trust)
            {
                Console.WriteLine("Adding to LocalMachine\\Root store (trusting)...");
                if (!storeService.TrustInMachineStore(tempPath, password))
                {
                    Console.Error.WriteLine("Failed to trust certificate in machine store. Ensure you are running as Administrator.");
                    return 1;
                }
                Console.WriteLine("Certificate trusted in LocalMachine\\Root store.");
            }

            Console.WriteLine("Done.");
            return 0;
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static int HandleMachineStoreCheck(bool trust, WindowsCertStoreService storeService)
    {
        Console.WriteLine("Checking machine store status...");

        var existsInMyStore = storeService.CheckMachineStore();

        if (!existsInMyStore)
        {
            Console.WriteLine("Certificate not found in LocalMachine\\My store.");
            return 2;
        }

        Console.WriteLine("Certificate exists in LocalMachine\\My store.");

        if (trust)
        {
            var isTrusted = storeService.CheckMachineTrust();
            if (!isTrusted)
            {
                Console.WriteLine("Certificate not trusted (not in LocalMachine\\Root store).");
                return 3;
            }
            Console.WriteLine("Certificate is trusted (exists in LocalMachine\\Root store).");
        }

        return 0;
    }

    private static async Task<int> HandleWslAsync(string? distro, bool trust, bool check, CancellationToken cancellationToken)
    {
        var certService = new DevCertService();
        var wslService = new WslService();

        // Determine which distro to use
        if (string.IsNullOrEmpty(distro))
        {
            distro = await wslService.GetDefaultDistroAsync(cancellationToken);
            if (string.IsNullOrEmpty(distro))
            {
                Console.Error.WriteLine("No default WSL distribution found.");
                return 1;
            }
            Console.WriteLine($"Using default WSL distribution: {distro}");
        }
        else
        {
            Console.WriteLine($"Using WSL distribution: {distro}");
        }

        // Check if dotnet is available in WSL
        Console.WriteLine("Checking if dotnet is available in WSL...");
        if (!await wslService.CheckDotnetAvailableAsync(distro, cancellationToken))
        {
            Console.Error.WriteLine($"dotnet CLI is not available in WSL distribution '{distro}'.");
            return 4;
        }

        if (check)
        {
            return await HandleWslCheckAsync(distro, trust, wslService, cancellationToken);
        }

        // Ensure cert exists on the Windows host
        Console.WriteLine("Checking host dev certificate status...");
        var status = await certService.CheckStatusAsync(cancellationToken);

        if (!status.Exists)
        {
            Console.WriteLine("Dev certificate not found on host. Creating...");
            if (!await certService.EnsureCreatedAsync(cancellationToken))
            {
                Console.Error.WriteLine("Failed to create dev certificate.");
                return 1;
            }
            Console.WriteLine("Dev certificate created.");
        }
        else
        {
            Console.WriteLine("Dev certificate exists on host.");
        }

        // Export cert to temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.pfx");
        var password = Guid.NewGuid().ToString("N");

        try
        {
            Console.WriteLine("Exporting certificate...");
            if (!await certService.ExportAsync(tempPath, CertificateFormat.Pfx, password, cancellationToken))
            {
                Console.Error.WriteLine("Failed to export dev certificate.");
                return 1;
            }

            // Import into WSL
            Console.WriteLine($"Importing certificate into WSL ({distro})...");
            if (!await wslService.ImportCertAsync(tempPath, password, trust, distro, cancellationToken))
            {
                Console.Error.WriteLine("Failed to import certificate into WSL.");
                return 1;
            }

            if (trust)
            {
                Console.WriteLine("Certificate imported and trusted in WSL.");
            }
            else
            {
                Console.WriteLine("Certificate imported into WSL.");
            }

            Console.WriteLine("Done.");
            return 0;
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static async Task<int> HandleWslCheckAsync(string distro, bool trust, WslService wslService, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Checking certificate status in WSL ({distro})...");

        var (exists, trusted) = await wslService.CheckCertStatusAsync(distro, cancellationToken);

        if (!exists)
        {
            Console.WriteLine("Certificate not found in WSL.");
            return 2;
        }

        Console.WriteLine("Certificate exists in WSL.");

        if (trust)
        {
            if (!trusted)
            {
                Console.WriteLine("Certificate not trusted in WSL.");
                return 3;
            }
            Console.WriteLine("Certificate is trusted in WSL.");
        }

        return 0;
    }
}
