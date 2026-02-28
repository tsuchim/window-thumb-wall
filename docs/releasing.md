# Release Process

1. **Create a GPG-signed tag**
   ```bash
   git tag -s v1.0 -m "Release v1.0"
   ```
2. **Push the tag**
   ```bash
   git push origin v1.0
   ```
3. **GitHub Actions builds artifacts** — the
   [`build-release-artifacts`](../.github/workflows/build-release-artifacts.yml)
   workflow produces ZIP, MSI, and MSIX packages.
4. **Only CI-built artifacts are used for releases** — no manually-built
   binaries are distributed.
5. *(Planned)* CI artifacts will be submitted to **SignPath Foundation** for
   code signing before being attached to a GitHub Release.
