# Build and Release Process

This document describes the CI/CD pipeline for dotnet-dev-certs-plus, including versioning, builds, and releases.

## Overview

The project uses four GitHub Actions workflows:

| Workflow | File | Trigger | Purpose |
|----------|------|---------|---------|
| **CI** | `ci.yml` | Push to `main` | Dev builds → GitHub Packages, RC artifacts |
| **Release** | `release.yml` | Manual dispatch | Ships RC from CI → NuGet.org |
| **Bump Version** | `bump-version.yml` | Manual dispatch | Change version base or phase |
| **PR** | `pr.yml` | Pull requests | Build verification |

### Key Concepts

- **CI builds both dev and RC packages** - Every push to main produces a dev package (pushed to GitHub Packages) and an RC package (stored as artifact)
- **Release just ships** - The Release workflow downloads the RC package from CI and publishes it, no rebuild
- **Bump Version for transitions** - Use the Bump Version workflow to change version base or move to a new phase

## Version Script

Version logic is centralized in `scripts/version.cs`, a C# file-based app with subcommands:

```bash
# Read version state from dev release
dotnet scripts/version.cs -- state --body "$RELEASE_BODY"

# Calculate dev and RC versions
dotnet scripts/version.cs -- calculate --state "$STATE_JSON"

# Advance state after shipping
dotnet scripts/version.cs -- advance --state "$STATE_JSON" --shipped-version "0.0.1-pre.1.rel"

# Bump version/phase
dotnet scripts/version.cs -- bump --state "$STATE_JSON" --version-bump auto --phase rtm

# Validate a version against shipped releases
dotnet scripts/version.cs -- validate --version "0.0.1" --releases-json "$RELEASES"
```

## Workflow Structure

### CI Workflow (`ci.yml`)

Runs automatically on every push to `main`:

1. **Calculate version** - Uses version script to determine dev and RC versions
2. **Build dev package** - Build with dev version
3. **Test** - Run unit tests
4. **Pack dev package** - Create dev NuGet package
5. **Build RC package** - Rebuild with RC version
6. **Pack RC package** - Create RC NuGet package
7. **Upload artifacts** - Store both `package` (dev) and `package-rc` (RC) artifacts
8. **Push to GitHub Packages** - Publish dev package only
9. **Update dev draft release** - Store version state and CI run ID

If a release is pending, the CI workflow skips publishing.

### Release Workflow (`release.yml`)

Triggered manually via workflow dispatch (no inputs required):

1. **Get version state** - Read current state from dev release
2. **Download RC package** - Get `package-rc` artifact from CI run
3. **Verify version** - Ensure package version matches expected RC version
4. **Push to NuGet.org** - Publish via trusted publishing
5. **Create GitHub release** - Create release with package attached
6. **Advance version state** - Auto-increment phase number or bump base version

The release type (prerelease vs stable) is determined automatically from the current phase:
- `pre` or `rc` phase → prerelease to NuGet
- `rtm` phase → stable to NuGet

### Bump Version Workflow (`bump-version.yml`)

Triggered manually to change version base or phase:

**Inputs:**
- `version_bump`: `none`, `auto`, `patch`, `minor`, or `major`
- `phase`: `pre`, `rc`, or `rtm`

**What it does:**
1. Validates the transition against shipped releases
2. Updates version state
3. Builds and publishes new dev/RC packages with the new version

Use this workflow to:
- Move from `pre` to `rc` or `rtm` phase
- Bump to a new major, minor, or patch version
- Both at once (e.g., bump major AND move to rtm)

### PR Workflow (`pr.yml`)

Runs on all pull requests:

1. **Build** - Uses the composite action

This ensures all PRs are buildable before merge.

## Versioning Scheme

### Version Format

| Phase | Dev Version | Shipped Version |
|-------|-------------|-----------------|
| pre | `0.0.1-pre.1.dev.5` | `0.0.1-pre.1.rel` |
| rc | `0.0.1-rc.1.dev.3` | `0.0.1-rc.1.rel` |
| rtm | `0.0.1-rtm.dev.2` | `0.0.1` |

### Version State

Version state is stored in the body of a draft GitHub release named `dev`:

```
<!-- VERSION_STATE: {base}|{phase}|{phase_number}|{dev_number}|{pending} -->
<!-- RC_VERSION: {rc_version} -->
<!-- CI_RUN_ID: {run_id} -->
```

Example: `<!-- VERSION_STATE: 0.0.1|pre|2|5|none -->`

Fields:
- **base**: The base semantic version (e.g., `0.0.1`)
- **phase**: `pre`, `rc`, or `rtm`
- **phase_number**: Current phase number (1, 2, 3...), 0 for rtm
- **dev_number**: Current dev build number
- **pending**: `none` or pending release type

## Release Phases

### Pre Phase (default)

For early development and iteration:
- Dev builds: `0.0.1-pre.1.dev.1`, `0.0.1-pre.1.dev.2`, ...
- Shipped: `0.0.1-pre.1.rel`
- After ship: phase number increments to `pre.2`

### RC Phase (optional)

For release candidates before stable:
- Dev builds: `0.0.1-rc.1.dev.1`, `0.0.1-rc.1.dev.2`, ...
- Shipped: `0.0.1-rc.1.rel`
- After ship: phase number increments to `rc.2`

### RTM Phase

For preparing the stable release:
- Dev builds: `0.0.1-rtm.dev.1`, `0.0.1-rtm.dev.2`, ...
- Shipped: `0.0.1` (stable!)
- After ship: base version bumps, phase resets to `pre.1`

## Example Timeline

1. Start: base=`0.0.1`, phase=`pre`, phase_number=1
2. CI runs, produces `0.0.1-pre.1.dev.1` (dev) and `0.0.1-pre.1.rel` (RC)
3. **Release** ships `0.0.1-pre.1.rel` → phase_number becomes 2
4. CI runs, produces `0.0.1-pre.2.dev.1` and `0.0.1-pre.2.rel`
5. **Release** ships `0.0.1-pre.2.rel` → phase_number becomes 3
6. **Bump Version** with phase=`rtm` → triggers build of `0.0.1-rtm.dev.1` and `0.0.1`
7. **Release** ships `0.0.1` → base becomes `0.0.2`, phase resets to `pre.1`

## How to Release

### Ship a Pre-release or Stable

Simply run the **Release** workflow - no inputs needed:

```bash
gh workflow run release.yml --repo DamianEdwards/dotnet-dev-certs-plus
```

The workflow automatically:
- Determines if it's a prerelease or stable based on current phase
- Downloads the RC package from CI
- Publishes to NuGet.org
- Advances the version state

### Change Phase or Version

Use the **Bump Version** workflow:

```bash
# Move to RTM phase (prepare for stable)
gh workflow run bump-version.yml -f version_bump=none -f phase=rtm

# Bump to next minor version
gh workflow run bump-version.yml -f version_bump=minor -f phase=pre

# Bump major and go straight to RTM
gh workflow run bump-version.yml -f version_bump=major -f phase=rtm
```

## Trusted Publishing

The workflow uses NuGet trusted publishing:

1. GitHub Actions requests an OIDC token
2. Token is exchanged with NuGet.org for a temporary API key
3. API key is used to push the package

**Requirements:**
- Trusted publishing policy configured on NuGet.org
- `id-token: write` permission in the Release workflow
- `nuget.org` environment configured in GitHub repository

## Troubleshooting

### Package version mismatch during release

The release workflow verifies the RC package version matches the expected version. If they don't match:
1. The version state may have been modified after the last CI run
2. Run CI again or use Bump Version to synchronize

### No RC artifact found

Ensure CI ran successfully. The `package-rc` artifact is created by CI and is required for release.

### Invalid phase transition

The Bump Version workflow validates transitions against shipped releases. You cannot:
- Go backwards in phase without bumping version (e.g., rtm → pre)
- Ship a version less than or equal to an existing release

### Version state is wrong

Manually edit the dev draft release body to correct the `VERSION_STATE` comment.

### No dev release exists

Workflows will initialize from the latest stable release, or start from `0.0.1-pre.1.dev.1` if none exists.
