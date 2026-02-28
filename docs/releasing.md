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
4. **Download CI artifacts and create a GitHub Release** — attach the
   CI-built packages as release assets. No locally-built binaries are
   distributed.
5. Build tooling versions are pinned for reproducibility:
   - .NET SDK is pinned via `global.json`
   - WiX CLI is pinned via `.config/dotnet-tools.json` (and the CI workflow)
6. *(Planned)* CI artifacts will be submitted to **SignPath Foundation** for
   code signing before being attached to a GitHub Release.
