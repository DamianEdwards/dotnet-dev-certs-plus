# dotnet-dev-certs-plus

Extended functionality for the `dotnet dev-certs` command, including machine store and WSL support.

## Installation

```bash
dotnet tool install --global dotnet-dev-certs-plus
```

## Features

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
```

> **Note:** The `dotnet` CLI must be installed in the WSL distribution.

## Command Reference

```
dotnet dev-certs-plus https [options]

Options:
  --store <machine>     Import cert to the machine store (Windows, Linux, macOS)
  --wsl [<distro>]      Import cert to WSL distro (Windows only)
  --trust               Trust the certificate
  --check               Check certificate status (don't create/import)
  -h, --help            Show help
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
