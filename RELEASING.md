# Release Process

This document describes the release management process for OrderManager Monolith.

## Overview

The project uses:
- **Conventional Commits** for structured commit messages
- **Semantic Versioning (SemVer)** for version numbering
- **git-cliff** for automated changelog generation
- **GitHub Actions** for CI/CD automation
- **GitHub Container Registry (ghcr.io)** for Docker image hosting

## Commit Convention

All commits should follow the [Conventional Commits](https://www.conventionalcommits.org) specification:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types

| Type | Description | Version Bump |
|------|-------------|--------------|
| `feat` | A new feature | Minor |
| `fix` | A bug fix | Patch |
| `docs` | Documentation only | None |
| `style` | Code style (formatting, etc.) | None |
| `refactor` | Code refactoring | None |
| `perf` | Performance improvement | Patch |
| `test` | Adding or updating tests | None |
| `chore` | Maintenance tasks | None |
| `ci` | CI/CD changes | None |
| `build` | Build system changes | None |
| `revert` | Reverting a previous commit | Patch |

### Breaking Changes

Append `!` after the type or include `BREAKING CHANGE:` in the footer for major version bumps:

```
feat!: remove deprecated /v1/orders endpoint

BREAKING CHANGE: The /v1/orders endpoint has been removed. Use /v2/orders instead.
```

## Versioning

Versions follow [Semantic Versioning 2.0.0](https://semver.org):

- **MAJOR** (`X.0.0`): Breaking API changes
- **MINOR** (`0.X.0`): New features, backwards compatible
- **PATCH** (`0.0.X`): Bug fixes, backwards compatible

## Creating a Release

### 1. Determine the next version

Review commits since the last release to determine the appropriate version bump:

```bash
# View commits since last tag
git log $(git describe --tags --abbrev=0)..HEAD --oneline
```

### 2. Create and push a version tag

```bash
# Tag the release (replace X.Y.Z with actual version)
git tag -a vX.Y.Z -m "Release vX.Y.Z"

# Push the tag to trigger the release workflow
git push origin vX.Y.Z
```

### 3. Automated release process

Once the tag is pushed, the release workflow automatically:

1. Builds the .NET 8 backend and Angular 17 frontend
2. Creates a multi-stage Docker image
3. Tags the image with:
   - Full version (e.g., `1.2.3`)
   - Minor version (e.g., `1.2`)
   - Major version (e.g., `1`)
   - `latest`
4. Pushes the image to GitHub Container Registry
5. Generates release notes from conventional commits
6. Creates a GitHub Release with the release notes

### 4. Verify the release

- Check the [Actions tab](../../actions) for workflow status
- Verify the [GitHub Release](../../releases) was created
- Confirm the Docker image is available:
  ```bash
  docker pull ghcr.io/cognition-partner-workshops/ordermanager-monolith:X.Y.Z
  ```

## Changelog

The `CHANGELOG.md` is automatically updated on every push to `main` by the changelog workflow. It aggregates all conventional commits grouped by type.

The changelog configuration is in `cliff.toml`.

## Docker Image

The Docker image is a multi-stage build:

1. **frontend-build**: Compiles the Angular app with `ng build --configuration production`
2. **backend-build**: Restores, builds, and publishes the .NET app with the frontend assets
3. **runtime**: Minimal ASP.NET runtime image serving on port 8080

### Running locally

```bash
docker pull ghcr.io/cognition-partner-workshops/ordermanager-monolith:latest
docker run -p 8080:8080 ghcr.io/cognition-partner-workshops/ordermanager-monolith:latest
```

## Hotfix Process

For urgent fixes to a released version:

1. Create a branch from the release tag: `git checkout -b hotfix/vX.Y.Z+1 vX.Y.Z`
2. Apply the fix with a `fix:` conventional commit
3. Merge to `main`
4. Tag the new patch version: `git tag -a vX.Y.(Z+1) -m "Release vX.Y.(Z+1)"`
5. Push the tag: `git push origin vX.Y.(Z+1)`

## First Release

For the initial release of the project:

```bash
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```
