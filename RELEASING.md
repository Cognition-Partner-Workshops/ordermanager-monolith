# Release Process

This project uses **semantic versioning** and **conventional commits** to automate changelog generation, Docker image builds, and GitHub Releases.

## Version Scheme

Versions follow [Semantic Versioning 2.0.0](https://semver.org/):

```
MAJOR.MINOR.PATCH
```

| Increment | When to use | Example commit |
|-----------|-------------|----------------|
| **MAJOR** | Breaking API or behavioral changes | `feat!: remove legacy order endpoint` |
| **MINOR** | New features, backward-compatible | `feat: add bulk order creation` |
| **PATCH** | Bug fixes, minor improvements | `fix: correct inventory count on return` |

## Conventional Commits

All commits should follow the [Conventional Commits](https://www.conventionalcommits.org/) specification. The changelog and release notes are generated automatically from these prefixes:

| Prefix | Purpose |
|--------|---------|
| `feat:` | A new feature |
| `fix:` | A bug fix |
| `docs:` | Documentation only |
| `style:` | Formatting, missing semicolons, etc. |
| `refactor:` | Code change that neither fixes a bug nor adds a feature |
| `perf:` | Performance improvement |
| `test:` | Adding or correcting tests |
| `build:` | Changes to the build system or dependencies |
| `ci:` | Changes to CI configuration |
| `chore:` | Other changes that don't modify src or test files |

Append `!` after the type (e.g., `feat!:`) or add a `BREAKING CHANGE:` footer to indicate a breaking change.

## Automated Changelog

A GitHub Actions workflow (`.github/workflows/changelog.yml`) runs on every push to `main` and regenerates `CHANGELOG.md` from the commit history. No manual editing of the changelog is required.

## Creating a Release

### Option A: Push a Git Tag (recommended)

```bash
# Ensure you are on main and up to date
git checkout main
git pull origin main

# Create an annotated tag
git tag -a v1.0.0 -m "Release v1.0.0"

# Push the tag to trigger the release workflow
git push origin v1.0.0
```

### Option B: Manual Workflow Dispatch

1. Go to **Actions > Release** in the GitHub UI.
2. Click **Run workflow**.
3. Enter the version (e.g., `1.0.0` — without the `v` prefix).
4. Click **Run workflow**.

## What the Release Workflow Does

When a version tag is pushed (or the workflow is dispatched manually), the **Release** workflow (`.github/workflows/release.yml`) performs these steps:

1. **Determines the version** from the git tag or manual input.
2. **Generates release notes** from conventional commits since the previous tag.
3. **Builds a multi-stage Docker image** containing both the .NET 8 API and the Angular 17 frontend.
4. **Pushes the image** to GitHub Container Registry (`ghcr.io`) with the following tags:
   - `<version>` (e.g., `1.0.0`)
   - `<major>.<minor>` (e.g., `1.0`)
   - `<major>` (e.g., `1`)
   - `latest`
5. **Creates a GitHub Release** with the generated release notes and Docker pull instructions.

## Pulling the Docker Image

After a release, the image is available at:

```bash
docker pull ghcr.io/cognition-partner-workshops/app_dotnet-angular-monolith:<version>
```

Run the container:

```bash
docker run -p 8080:8080 ghcr.io/cognition-partner-workshops/app_dotnet-angular-monolith:<version>
```

The application will be available at `http://localhost:8080`.
