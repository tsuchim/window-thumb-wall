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

1. An ordinary push of an annotated tag named `vX.Y.Z` starts the release.
   Sign it using the repository's trusted SSH signing key. The trust list is
   [release-allowed-signers](../.github/release-allowed-signers) on protected main.
   The workflow verifies the signature, tag object, target commit and source tree.
2. GitHub Actions runs the required Windows tests and builds both architectures.
   Its release provenance lists the toolchain, package paths and SHA-256 hashes.
3. After those checks pass, CI builds and submits the Microsoft Store package.
   A successful submission and a second verification of the tag and artifacts
   allow CI to create the draft GitHub Release with the CI-built ZIP, MSI
   and MSIX assets.
4. The maintainer verifies the draft and promotes it to a public GitHub Release.
   Microsoft Store certification proceeds separately after submission.
5. **No locally or manually-built binaries are distributed.**
6. Once SignPath integration is complete, CI artifacts will be submitted to
   SignPath for signing before publication.

## Security

- GitHub MFA is enabled for all maintainers.
- Release tags require a verified annotated SSH signature from the protected trust list.
- Workflow permissions follow the principle of least privilege.

## Privacy

This project **does not intentionally collect any personal data**.
Current releases do not include telemetry.
