# Invariants

These rules define the non-negotiable project constraints for WindowThumbWall.

## Packaging

- Official distribution formats are limited to ZIP, MSI, and MSIX.
- Changes to packaging, CI, documentation, or release flow must preserve support for all three formats.

## Release provenance

- Only CI-built release artifacts are distributed.
- Tag-driven GitHub Actions builds are the source of truth for release packages.
- Locally built binaries are for development and validation only.

## Product scope

- The application displays live DWM thumbnails of external windows.
- It is not a screen-capture or streaming application.

## Platform

- Windows is the supported runtime platform.
- The desktop application stack is WPF on .NET 10.