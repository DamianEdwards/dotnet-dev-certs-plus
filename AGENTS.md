# AGENTS.md

This file provides context for AI coding assistants working on this repository.

## Project Overview

**dotnet-dev-certs-plus** is a .NET global tool that extends `dotnet dev-certs https` with additional functionality:
- **Machine store support** (`--store machine`) - Import certificates to system-wide stores
- **WSL support** (`--wsl`) - Import certificates from Windows host into WSL distributions

The tool is a drop-in replacement for `dotnet dev-certs https` - all standard options pass through to the underlying command.

## Build Commands

```bash
# Restore dependencies
dotnet restore src/dotnet-dev-certs-plus

# Build
dotnet build src/dotnet-dev-certs-plus

# Run directly
dotnet run --project src/dotnet-dev-certs-plus -- https --help

# Pack as global tool
dotnet pack src/dotnet-dev-certs-plus

# Install locally for testing
dotnet tool install --global --add-source ./src/dotnet-dev-certs-plus/bin/Release dotnet-dev-certs-plus
```

## Architecture

```
src/dotnet-dev-certs-plus/
├── Program.cs                 # Entry point, sets up System.CommandLine
├── Commands/
│   └── HttpsCommand.cs        # Main command implementation, handles all CLI options
├── Services/
│   ├── DevCertService.cs      # Interacts with dotnet dev-certs for cert operations
│   ├── MachineStoreService.cs # Platform-specific machine store operations
│   ├── WslService.cs          # WSL distribution detection and cert import
│   └── ProcessRunner.cs       # Process execution helper
└── Models/                    # Data models for command results
```

### Key Components

- **HttpsCommand**: Parses CLI arguments, delegates to appropriate service based on `--store` or `--wsl` flags
- **DevCertService**: Wraps `dotnet dev-certs https` for certificate creation, export, and trust
- **MachineStoreService**: Platform-specific logic for Windows (certificate stores), Linux (`update-ca-certificates`), and macOS (System Keychain)
- **WslService**: Executes commands in WSL distributions via `wsl.exe`

## Coding Conventions

- **Target Framework**: .NET 10
- **Language**: C# with nullable reference types enabled
- **Implicit usings**: Enabled
- **CLI Framework**: System.CommandLine 2.0.0-beta5
- **Packaging**: .NET global tool (`PackAsTool=true`)

## Release Process

See [docs/build-and-release.md](docs/build-and-release.md) for:
- Versioning scheme (dev builds, pre-releases, stable releases)
- CI/CD pipeline details
- How to trigger releases via GitHub Actions
