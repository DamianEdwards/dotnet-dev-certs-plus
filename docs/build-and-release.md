# Build and Release Process

This document describes the CI/CD pipeline for dotnet-dev-certs-plus, including versioning, builds, and releases.

## Overview

The project uses three GitHub Actions workflows:

| Workflow | File | Trigger | Purpose |
|----------|------|---------|---------|
| **CI** | `ci.yml` | Push to `main` | Dev builds → GitHub Packages, RC artifacts for release |
| **Release** | `release.yml` | Manual dispatch | Downloads RC from CI → NuGet.org |
| **PR** | `pr.yml` | Pull requests | Build verification |

The CI workflow builds both a dev package (published to GitHub Packages) and a release candidate (RC) package (stored as an artifact). The Release workflow downloads the RC package from the latest CI run rather than rebuilding, ensuring releases use the exact tested binaries.

## Workflow Structure

### Composite Action (`.github/actions/build/`)

A reusable action that performs the common build steps:

1. **Checkout** - Clone the repository with full history
2. **Setup .NET** - Install .NET 10 SDK (preview)
3. **Restore** - Restore NuGet packages
4. **Build** - Build in Release configuration

This action is used by all three workflows for consistency.

### CI Workflow (`ci.yml`)

Runs automatically on every push to `main`:

1. **Calculate version** - Determine dev version and RC version from state
2. **Build** - Uses the composite action with dev version
3. **Test** - Run unit tests
4. **Pack dev package** - Create dev NuGet package
5. **Build RC package** - Rebuild with RC version for correct assembly metadata
6. **Pack RC package** - Create RC NuGet package
7. **Upload artifacts** - Store both dev (`package`) and RC (`package-rc`) packages
8. **Push to GitHub Packages** - Publish dev package only
9. **Update dev draft release** - Store version state and CI run ID

The RC version is determined by the current stage:
- **pre stage**: RC version is `{base}-pre.{pre}.rel` (e.g., `0.0.1-pre.1.rel`)
- **rtm stage**: RC version is `{base}` (e.g., `0.0.1`)

If a release is pending (indicated by `pending_release` in state), the CI workflow skips publishing to avoid version conflicts.

### Release Workflow (`release.yml`)

Triggered manually via workflow dispatch:

**Inputs:**
- `release_type`: `prerelease`, `rtm`, or `stable` (required)
- `version_bump`: `none`, `patch`, `minor`, or `major` (optional, but requires a new CI run after bumping)

**Jobs:**

1. **Prepare job**:
   - Calculates expected release version from state
   - Finds the CI run ID (from dev release state or latest successful CI run)
   - Updates dev release with pending state

2. **Publish job** (for prerelease/stable):
   - Downloads `package-rc` artifact from the CI run
   - Verifies package version matches expected release version
   - Pushes to NuGet.org via trusted publishing
   - Creates GitHub release
   - Confirms version state after success

3. **Confirm-RTM job** (for rtm):
   - Confirms RTM state (no NuGet publish needed)

**Important:** The Release workflow does not build - it downloads the RC package from the CI workflow. This ensures releases use the exact binaries that were tested.

### PR Workflow (`pr.yml`)

Runs on all pull requests:

1. **Build** - Uses the composite action

This ensures all PRs are buildable before merge. Tests will be added here when available.

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

Version state is stored in the body of a draft GitHub release named `dev`. The state is stored as HTML comments:

```
<!-- VERSION_STATE: {base_version}|{stage}|{pre_number}|{dev_number}|{pending_release} -->
<!-- RC_VERSION: {rc_version} -->
<!-- CI_RUN_ID: {run_id} -->
```

Example: `<!-- VERSION_STATE: 0.0.1|pre|2|5|none -->`

Fields:
- **base_version**: The base semantic version (e.g., `0.0.1`)
- **stage**: Either `pre` or `rtm`
- **pre_number**: Current pre-release number
- **dev_number**: Current dev build number within the pre-release
- **pending_release**: `none`, `prerelease`, or `stable` (used for release retry handling)
- **rc_version**: The RC version built by CI (used for display)
- **ci_run_id**: The GitHub Actions run ID (used by release workflow to download artifacts)

## Build Types

### Development Builds (Automatic)

Triggered automatically on every push to `main`.

**What happens:**
1. Version is calculated from current state (incrementing `dev_number`)
2. Dev package is built and pushed to GitHub Packages
3. RC package is built (with release version) and stored as artifact
4. Dev draft release is updated with new state and CI run ID

**Version example progression:**
- `0.0.1-pre.1.dev.1` → `0.0.1-pre.1.dev.2` → `0.0.1-pre.1.dev.3`

**Installing dev builds:**
```bash
dotnet tool install --global dotnet-dev-certs-plus \
  --version 0.0.1-pre.1.dev.5 \
  --add-source https://nuget.pkg.github.com/DamianEdwards/index.json
```

### Pre-releases (Manual)

Triggered via **Release** workflow dispatch with `release_type: prerelease`.

**What happens:**
1. Release workflow downloads RC package from latest CI run
2. Version is verified to match `{base}-pre.{pre}.rel`
3. Package is pushed to NuGet.org
4. GitHub release is created (marked as pre-release)
5. State is updated: `pre_number` increments, `dev_number` resets to 0

**Version example:**
- Before: `0.0.1-pre.1.dev.5`
- Release: `0.0.1-pre.1.rel`
- After: `0.0.1-pre.2.dev.1`

### RTM Stage (Manual)

Triggered via **Release** workflow dispatch with `release_type: rtm`.

**What happens:**
1. Stage switches from `pre` to `rtm`
2. Subsequent dev builds use RTM versioning

**Version example:**
- Before: `0.0.1-pre.3.dev.2`
- After RTM: `0.0.1-rtm.dev.1` → `0.0.1-rtm.dev.2`

This is useful when you're feature-complete and only doing bug fixes before stable release.

### Stable Releases (Manual)

Triggered via **Release** workflow dispatch with `release_type: stable`.

**What happens:**
1. Release workflow downloads RC package from latest CI run
2. Version is verified to match `{base}` (no suffix)
3. Package is pushed to NuGet.org
4. GitHub release is created (not marked as pre-release)
5. State is updated: base version increments, resets to `pre.1.dev.0`

**Version example:**
- Before: `0.0.1-rtm.dev.3`
- Release: `0.0.1`
- After: `0.0.2-pre.1.dev.1`

## Version Bumps

Version bumps work differently with the new release workflow:

1. Version bumps are no longer applied during release
2. To bump versions, update the version state manually in the dev draft release
3. Push a commit to main to trigger a CI build with the new version
4. Then trigger the release workflow

| Bump Type | Before | After |
|-----------|--------|-------|
| `patch` | `0.0.1` | `0.0.2` |
| `minor` | `0.0.1` | `0.1.0` |
| `major` | `0.1.0` | `1.0.0` |

After a version bump, the state resets to `pre.1.dev.0`.

## Trusted Publishing

The workflow uses NuGet trusted publishing instead of API keys:

1. GitHub Actions requests an OIDC token
2. Token is exchanged with NuGet.org for a temporary API key
3. API key is used to push the package

**Requirements:**
- Trusted publishing policy configured on NuGet.org
- `id-token: write` permission in the Release workflow
- `nuget.org` environment configured in GitHub repository

## Pending Release Handling

To prevent version skips when a publish fails, the workflows use a "pending release" mechanism:

1. **Release workflow** stores state with `pending_release` flag (e.g., `prerelease`)
2. **CI workflow** detects pending state and skips dev builds
3. **Publish job** (after success) updates state with incremented values and clears pending flag
4. **If publish fails**, re-running the Release workflow detects the pending state and reuses the same version

This ensures that a failed release can be retried without losing the version number.

## How to Release

### Pre-release

1. Go to **Actions** → **Release** workflow
2. Click **Run workflow**
3. Select:
   - Branch: `main`
   - Release type: `prerelease`
   - Version bump: `none` (or select bump if needed)
4. Click **Run workflow**

### Stable Release

1. Optionally trigger RTM stage first (if not already in RTM)
2. Go to **Actions** → **Release** workflow
3. Click **Run workflow**
4. Select:
   - Branch: `main`
   - Release type: `stable`
   - Version bump: `none`
5. Click **Run workflow**

### Using the GitHub CLI

You can also trigger releases from the command line using the [GitHub CLI](https://cli.github.com/):

**Pre-release:**
```bash
gh workflow run release.yml -f release_type=prerelease -f version_bump=none --repo DamianEdwards/dotnet-dev-certs-plus
```

**Stable release:**
```bash
gh workflow run release.yml -f release_type=stable -f version_bump=none --repo DamianEdwards/dotnet-dev-certs-plus
```

**RTM stage:**
```bash
gh workflow run release.yml -f release_type=rtm -f version_bump=none --repo DamianEdwards/dotnet-dev-certs-plus
```

**Check workflow status:**
```bash
gh run list --workflow="release.yml" --repo DamianEdwards/dotnet-dev-certs-plus --limit 1
```

**Watch a run:**
```bash
gh run watch <run-id> --repo DamianEdwards/dotnet-dev-certs-plus
```

## Troubleshooting

### Publish job failed but NuGet push succeeded

The Release workflow uses `--skip-duplicate` so you can safely re-run. The NuGet push will skip the existing package and continue to the state confirmation step.

### Version state is wrong

You can manually edit the dev draft release body to correct the `VERSION_STATE` comment. The format is:

```
<!-- VERSION_STATE: {major}.{minor}.{patch}|{stage}|{pre_number}|{dev_number}|{pending_release} -->
```

### No dev release exists

If the dev draft release is missing, the workflows will:
1. Look for the latest stable release to determine base version
2. If no stable release exists, start from `0.0.1-pre.1.dev.1`

### Package version mismatch during release

If the release workflow fails with a version mismatch error, it means the RC package from CI doesn't match the expected release version. This can happen if:
- The version state was manually modified after the last CI run
- The wrong CI run was selected

**Solution:** Push a new commit to main to trigger a fresh CI build, then retry the release.

### No RC artifact found

If the release workflow can't find the `package-rc` artifact, ensure:
1. The CI workflow completed successfully
2. The CI run is recent (artifacts expire after 90 days)
3. The CI run wasn't skipped due to a pending release

### Version bump requested during release

Version bumps now require a new CI build. If you try to use `version_bump` in the release workflow, it will fail with an error. To bump versions:
1. Manually update the version state in the dev draft release
2. Push a commit to main to trigger CI
3. Run the release workflow with `version_bump: none`
