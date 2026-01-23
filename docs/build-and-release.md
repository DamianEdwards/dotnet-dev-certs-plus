# Build and Release Process

This document describes the CI/CD pipeline for dotnet-dev-certs-plus, including versioning, builds, and releases.

## Overview

The project uses a single GitHub Actions workflow (`ci.yml`) that handles:

- **Continuous Integration**: Automatic builds on every push to `main`
- **Development Packages**: Published to GitHub Packages for testing
- **Releases**: Published to NuGet.org via manual workflow dispatch

## Versioning Scheme

The project uses a custom versioning scheme that tracks state in a GitHub draft release.

### Version Format

| Build Type | Format | Example |
|------------|--------|---------|
| Dev build (pre stage) | `{major}.{minor}.{patch}-pre.{pre}.dev.{dev}` | `0.0.1-pre.1.dev.5` |
| Dev build (RTM stage) | `{major}.{minor}.{patch}-rtm.dev.{dev}` | `0.0.1-rtm.dev.3` |
| Pre-release | `{major}.{minor}.{patch}-pre.{pre}.rel` | `0.0.1-pre.1.rel` |
| Stable | `{major}.{minor}.{patch}` | `0.0.1` |

### Version State

Version state is stored in the body of a draft GitHub release named `dev`. The state is stored as an HTML comment:

```
<!-- VERSION_STATE: {base_version}|{stage}|{pre_number}|{dev_number}|{pending_release} -->
```

Example: `<!-- VERSION_STATE: 0.0.1|pre|2|5|none -->`

Fields:
- **base_version**: The base semantic version (e.g., `0.0.1`)
- **stage**: Either `pre` or `rtm`
- **pre_number**: Current pre-release number
- **dev_number**: Current dev build number within the pre-release
- **pending_release**: `none`, `prerelease`, or `stable` (used for release retry handling)

## Build Types

### Development Builds (Automatic)

Triggered automatically on every push to `main`.

**What happens:**
1. Version is calculated from current state (incrementing `dev_number`)
2. Package is built and packed
3. Package is pushed to GitHub Packages
4. Dev draft release is updated with new state

**Version example progression:**
- `0.0.1-pre.1.dev.1` → `0.0.1-pre.1.dev.2` → `0.0.1-pre.1.dev.3`

**Installing dev builds:**
```bash
dotnet tool install --global dotnet-dev-certs-plus \
  --version 0.0.1-pre.1.dev.5 \
  --add-source https://nuget.pkg.github.com/DamianEdwards/index.json
```

### Pre-releases (Manual)

Triggered via workflow dispatch with `release_type: prerelease`.

**What happens:**
1. Version is set to `{base}-pre.{pre}.rel`
2. Package is built and pushed to NuGet.org
3. GitHub release is created (marked as pre-release)
4. State is updated: `pre_number` increments, `dev_number` resets to 1

**Version example:**
- Before: `0.0.1-pre.1.dev.5`
- Release: `0.0.1-pre.1.rel`
- After: `0.0.1-pre.2.dev.1`

### RTM Stage (Manual)

Triggered via workflow dispatch with `release_type: rtm`.

**What happens:**
1. Stage switches from `pre` to `rtm`
2. Subsequent dev builds use RTM versioning

**Version example:**
- Before: `0.0.1-pre.3.dev.2`
- After RTM: `0.0.1-rtm.dev.1` → `0.0.1-rtm.dev.2`

This is useful when you're feature-complete and only doing bug fixes before stable release.

### Stable Releases (Manual)

Triggered via workflow dispatch with `release_type: stable`.

**What happens:**
1. Version is set to `{base}` (no suffix)
2. Package is built and pushed to NuGet.org
3. GitHub release is created (not marked as pre-release)
4. State is updated: base version increments, resets to `pre.1.dev.1`

**Version example:**
- Before: `0.0.1-rtm.dev.3`
- Release: `0.0.1`
- After: `0.0.2-pre.1.dev.1`

## Version Bumps

You can bump the major, minor, or patch version using the `version_bump` workflow input.

| Bump Type | Before | After |
|-----------|--------|-------|
| `patch` | `0.0.1` | `0.0.2` |
| `minor` | `0.0.1` | `0.1.0` |
| `major` | `0.1.0` | `1.0.0` |

After a version bump, the state resets to `pre.1.dev.1`.

## Workflow Jobs

### Build Job

Runs on every workflow execution:

1. **Checkout** - Clone the repository
2. **Setup .NET** - Install .NET 10 SDK (preview)
3. **Calculate version** - Determine version from state
4. **Restore** - Restore NuGet packages
5. **Build** - Build in Release configuration
6. **Pack** - Create NuGet package
7. **Upload artifact** - Store package for publish job
8. **Push to GitHub Packages** - Dev builds only
9. **Update dev draft release** - Store new version state

### Publish Job

Runs only for releases (`is_release == true`):

1. **Checkout** - Clone repository (needed for `gh` CLI)
2. **Download artifact** - Get package from build job
3. **Setup .NET** - Install .NET SDK
4. **NuGet login** - Authenticate via trusted publishing (OIDC)
5. **Push to NuGet.org** - Publish package
6. **Create GitHub release** - Create versioned release with notes
7. **Confirm version state** - Update dev release with incremented state

## Trusted Publishing

The workflow uses NuGet trusted publishing instead of API keys:

1. GitHub Actions requests an OIDC token
2. Token is exchanged with NuGet.org for a temporary API key
3. API key is used to push the package

**Requirements:**
- Trusted publishing policy configured on NuGet.org
- `id-token: write` permission in workflow
- `nuget.org` environment configured in GitHub repository

## Pending Release Handling

To prevent version skips when a publish fails, the workflow uses a "pending release" mechanism:

1. **Build job** stores state with `pending_release` flag (e.g., `prerelease`)
2. **Publish job** (after success) updates state with incremented values and clears pending flag
3. **If publish fails**, re-running the workflow detects the pending state and reuses the same version

This ensures that a failed release can be retried without losing the version number.

## How to Release

### Pre-release

1. Go to **Actions** → **CI** workflow
2. Click **Run workflow**
3. Select:
   - Branch: `main`
   - Release type: `prerelease`
   - Version bump: `none` (or select bump if needed)
4. Click **Run workflow**

### Stable Release

1. Optionally trigger RTM stage first (if not already in RTM)
2. Go to **Actions** → **CI** workflow
3. Click **Run workflow**
4. Select:
   - Branch: `main`
   - Release type: `stable`
   - Version bump: `none`
5. Click **Run workflow**

## Troubleshooting

### Publish job failed but NuGet push succeeded

The workflow uses `--skip-duplicate` so you can safely re-run the entire workflow. The NuGet push will skip the existing package and continue to the state confirmation step.

### Version state is wrong

You can manually edit the dev draft release body to correct the `VERSION_STATE` comment. The format is:

```
<!-- VERSION_STATE: {major}.{minor}.{patch}|{stage}|{pre_number}|{dev_number}|{pending_release} -->
```

### No dev release exists

If the dev draft release is missing, the workflow will:
1. Look for the latest stable release to determine base version
2. If no stable release exists, start from `0.0.1-pre.1.dev.1`
