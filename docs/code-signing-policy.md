# Code Signing Policy

## Overview

WindowThumbWall releases will be signed using
[SignPath Foundation](https://signpath.org/) (planned).
Until signing is integrated, release artifacts are distributed **unsigned**.

Local developer MSIX builds are different from published release artifacts:
- `packaging/build-msix.ps1` may generate a reusable self-signed certificate for local sideload testing.
- `packaging/install-msix.ps1` imports that local test certificate into the current user's trust stores for local installation only.
- That certificate is only for local installation and must not be treated as a release-signing mechanism.

## Roles

This is a single-maintainer project. All roles are held by **@tsuchim**.

| Role | Responsibility | Assignee |
|------|---------------|----------|
| **Author** | Writes code, creates signed tags | @tsuchim |
| **Reviewer** | Reviews changes before release | @tsuchim |
| **Approver** | Approves release builds for signing | @tsuchim |

## Signing Process

1. Release builds are triggered by tags matching `v*`.
   Maintainers **must** GPG-sign release tags (`git tag -s vX.Y`).
   > Note: GPG signature is an operational requirement.
   > The workflow does not technically verify tag signatures.
2. Builds run on **GitHub Actions** (`windows-latest`).
3. CI builds produce **ZIP**, **MSI**, and **MSIX** artifacts and upload them
   as workflow artifacts.
4. Publishing to **GitHub Releases** is currently performed **manually**:
   download CI-built artifacts from the workflow run and attach them as
   release assets.
5. **No locally or manually-built binaries are distributed.**
6. Once SignPath integration is complete, CI artifacts will be submitted to
   SignPath for signing before publication.

## Security

- GitHub MFA is enabled for all maintainers.
- Release tags **must** be GPG-signed (`git tag -s`).
- Workflow permissions follow the principle of least privilege.

## Privacy

This project **does not intentionally collect any personal data**.
Current releases do not include telemetry.
