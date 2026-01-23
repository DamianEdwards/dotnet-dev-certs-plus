# dotnet-dev-certs-plus

Extended functionality for the `dotnet dev-certs` command, including machine store and WSL support. This tool is a drop-in replacement for `dotnet dev-certs https` - all standard options are supported by passing through to the underlying command.

## Installation

```bash
dotnet tool install --global dotnet-dev-certs-plus
```

## Features

### Full `dotnet dev-certs https` Compatibility

All standard `dotnet dev-certs https` options are supported. When no extended options (`--store` or `--wsl`) are specified, commands pass through directly to `dotnet dev-certs https`:

```bash
# Check if certificate exists
dotnet dev-certs-plus https --check

# Create and trust certificate
dotnet dev-certs-plus https --trust

# Export certificate to a file
dotnet dev-certs-plus https --export-path ./cert.pfx --password mypassword

# Export as PEM without password
dotnet dev-certs-plus https --export-path ./cert.pem --format Pem --no-password

# Import a certificate
dotnet dev-certs-plus https --import ./cert.pfx --password mypassword

# Clean all certificates
dotnet dev-certs-plus https --clean

# Check with machine-readable output
dotnet dev-certs-plus https --check-trust-machine-readable
```

### Machine Store Support (`--store machine`)

Import the ASP.NET Core HTTPS development certificate to the system-wide certificate store. This is useful for:

- Running ASP.NET Core applications as services
- Scenarios where the certificate needs to be available machine-wide
- Development with IIS or other services running under different user accounts

```bash
# Import to machine store
dotnet dev-certs-plus https --store machine

# Import and trust
dotnet dev-certs-plus https --store machine --trust

# Check if certificate exists in machine store
dotnet dev-certs-plus https --store machine --check

# Check if certificate exists and is trusted
dotnet dev-certs-plus https --store machine --check --trust

# Get certificate status as JSON (for automation)
dotnet dev-certs-plus https --store machine --check-trust-machine-readable

# Remove certificate from machine store
dotnet dev-certs-plus https --store machine --clean

# Remove without confirmation prompt
dotnet dev-certs-plus https --store machine --clean --force
```

> **Note:** Machine store operations require elevated privileges (Administrator on Windows, sudo on Linux/macOS).

#### Platform-specific behavior

| Platform | Import Location | Trust Location |
|----------|-----------------|----------------|
| Windows | `LocalMachine\My` store | `LocalMachine\Root` store |
| Linux | `/usr/local/share/ca-certificates/` | Same (runs `update-ca-certificates`) |
| macOS | System Keychain | System Keychain with trust settings |

### WSL Support (`--wsl`)

Import the ASP.NET Core HTTPS development certificate from your Windows host into a WSL distribution. This enables HTTPS development scenarios where you're running .NET applications inside WSL but want to use the same certificate as your Windows host.

```bash
# Import to default WSL distribution
dotnet dev-certs-plus https --wsl

# Import and trust in WSL
dotnet dev-certs-plus https --wsl --trust

# Import to a specific WSL distribution
dotnet dev-certs-plus https --wsl ubuntu

# Import and trust in a specific distribution
dotnet dev-certs-plus https --wsl ubuntu --trust

# Check certificate status in WSL
dotnet dev-certs-plus https --wsl --check

# Check in specific distribution
dotnet dev-certs-plus https --wsl ubuntu --check --trust

# Get certificate status as JSON (for automation)
dotnet dev-certs-plus https --wsl --check-trust-machine-readable

# Remove certificate from default WSL distribution
dotnet dev-certs-plus https --wsl --clean

# Remove certificate from specific distribution
dotnet dev-certs-plus https --wsl ubuntu --clean

# Remove without confirmation prompt
dotnet dev-certs-plus https --wsl --clean --force
```

> **Note:** The `dotnet` CLI must be installed in the WSL distribution.

## Command Reference

```
dotnet dev-certs-plus https [options]

Standard Options (passed through to dotnet dev-certs https):
  -ep, --export-path <path>       Full path to the exported certificate
  -p, --password <password>       Password for PFX export or PEM key encryption
  -np, --no-password              No password for PEM export
  --format <Pfx|Pem>              Export format (default: Pfx)
  -i, --import <path>             Import certificate from file
  -c, --check                     Check certificate status (don't create)
  --clean                         Remove all HTTPS development certificates
  -t, --trust                     Trust the certificate
  -v, --verbose                   Display more debug information
  -q, --quiet                     Display warnings and errors only
  --check-trust-machine-readable  Output check --trust results as JSON

Extended Options:
  --store <machine>               Import cert to the machine store
  --wsl [<distro>]                Import cert to WSL distro (Windows only)
  --force                         Skip confirmation prompt when cleaning

  -h, --help                      Show help
```

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 1 | General error |
| 2 | Certificate not found (with --check) |
| 3 | Certificate not trusted (with --check --trust) |
| 4 | dotnet not available in WSL |

## How It Works

### Machine Store

1. Checks if the dev certificate exists on the host, creating it if necessary
2. Exports the certificate to a temporary file
3. Imports to the system certificate store:
   - **Windows**: `LocalMachine\My` store (with private key)
   - **Linux**: `/usr/local/share/ca-certificates/` as PEM
   - **macOS**: System Keychain
4. If `--trust` is specified:
   - **Windows**: Also imports to `LocalMachine\Root` store (without private key)
   - **Linux**: Runs `update-ca-certificates` to update system trust
   - **macOS**: Adds trust settings for SSL
5. Cleans up the temporary file

### WSL

1. Checks if the dev certificate exists on the Windows host, creating it if necessary
2. Exports the certificate to a temporary file
3. Runs `dotnet dev-certs https --import` inside the WSL distribution
4. If `--trust` is specified, includes `--trust` in the WSL command
5. Cleans up the temporary file

## Requirements

- .NET 10 SDK or later
- Elevated privileges for machine store operations:
  - **Windows**: Run as Administrator
  - **Linux**: Use `sudo` (requires `update-ca-certificates` command)
  - **macOS**: Use `sudo` (uses `security` command)
- Windows only for WSL features
- WSL with .NET SDK installed (for WSL features)

## License

MIT
