# Release Process

1. **Create a GPG-signed tag**
   ```bash
   git tag -s v1.2.3 -m "Release v1.2.3"
   ```
2. **Push the tag**
   ```bash
   git push origin v1.2.3
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

## Microsoft Store Automation (Plan A)

For existing live free products on the Microsoft Store, pushing a tag with the format `vX.Y.Z` or `vX.Y.Z.W` triggers an automated submission via [release-to-store.yml](../.github/workflows/release-to-store.yml).

Plan A only applies to already-live free Store products.

Three-component tags are normalized to four-part package versions for MSIX submissions. For example, `v0.4.2` becomes `0.4.2.0` inside the Store workflow.

### Prerequisites

1. **GitHub Secrets**:
   - `AZURE_AD_TENANT_ID`
   - `AZURE_AD_APPLICATION_CLIENT_ID`
   - `AZURE_AD_APPLICATION_SECRET`
   - `SELLER_ID`
2. **GitHub Variable**:
   - `MSSTORE_PRODUCT_ID`: The Store ID (Product ID) of the app.

### Workflow Trigger
Pushing a tag like `v0.4.2` or `v0.4.2.1` (validated against `^v\d+\.\d+\.\d+(\.\d+)?$`) will:
1. Build the MSIX package.
2. Normalize the package version to four numeric components and patch that value into `AppxManifest.xml`.
3. Create a draft submission in the Microsoft Store using `microsoft/microsoft-store-apppublisher` and `msstore publish --noCommit`.

Remove `--noCommit` from [../.github/workflows/release-to-store.yml](../.github/workflows/release-to-store.yml) after the first dry run succeeds and the draft submission looks correct in Partner Center.

**Caution**: Avoid mixing manual edits in the Partner Center portal with CLI-based submissions while a submission is in progress.
