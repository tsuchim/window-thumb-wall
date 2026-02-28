# Code Signing Policy

## Overview

WindowThumbWall releases will be signed using
[SignPath Foundation](https://signpath.org/) (planned).
Until signing is integrated, release artifacts are distributed **unsigned**.

## Roles

This is a single-maintainer project. All roles are held by **@tsuchim**.

| Role | Responsibility | Assignee |
|------|---------------|----------|
| **Author** | Writes code, creates signed tags | @tsuchim |
| **Reviewer** | Reviews changes before release | @tsuchim |
| **Approver** | Approves release builds for signing | @tsuchim |

## Signing Process

1. Release builds are triggered by GPG-signed tags matching `v*`
   (`git tag -s vX.Y`).
2. Builds run on **GitHub Actions** (`windows-latest`).
3. Only **CI-built artifacts** are published in GitHub Releases.
4. **No manually-built binaries are distributed.**
5. Once SignPath integration is complete, CI artifacts will be submitted to
   SignPath for signing before publication.

## Security

- GitHub MFA is enabled for all maintainers.
- Release tags **must** be GPG-signed (`git tag -s`).
- Workflow permissions follow the principle of least privilege.

## Privacy

This project **does not collect any personal data**.
No telemetry, analytics, or network requests are made by the application.
