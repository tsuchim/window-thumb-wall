# Release Process

WindowThumbWall uses a tag-driven release flow. Pushing a version tag builds all three official package formats and generates release metadata for GitHub Releases and Microsoft Store copy.

## Distribution Invariant
The official distribution formats are fixed.

- ZIP: `WindowThumbWall-vX.Y.Z-win-x64.zip`
- MSI: `WindowThumbWall-vX.Y.Z-win-x64.msi`
- MSIX: `WindowThumbWall-vX.Y.Z-win-x64.msix`

Do not change this set without explicitly revisiting [invariants.md](invariants.md).

## Prepare Release-Specific Highlights
If you want version-specific notes, create one of the following before tagging.

Example:
```text
packaging/release-notes-0.2.3.md
packaging/release-notes-ja-0.2.3.md
packaging/release-notes-en-0.2.3.md
```

How they are used:
- `release-notes-<version>.md`: shared fallback for both languages
- `release-notes-ja-<version>.md`: overrides Japanese Store metadata and Japanese "What's new"
- `release-notes-en-<version>.md`: overrides GitHub Release text, English Store metadata, and English "What's new"

Each file can contain short markdown bullets or short prose describing what changed in that release. The metadata generator embeds that content into the generated release assets.

## Tag and Push
1. Create a signed tag.

```bash
git tag -s v0.2.3 -m "Release v0.2.3"
```

2. Push the tag.

```bash
git push origin v0.2.3
```

## What The Build Workflow Generates
The [build-release-artifacts workflow](../.github/workflows/build-release-artifacts.yml) runs [packaging/generate-release-metadata.ps1](../packaging/generate-release-metadata.ps1) on each tagged release.

Generated files:
- `dist/release-notes.md`: GitHub Release body
- `dist/store-listing-ja.md`: Japanese Store listing copy
- `dist/store-listing-en.md`: English Store listing copy
- `dist/store-whats-new-ja.md`: Japanese "What's new" copy
- `dist/store-whats-new-en.md`: English "What's new" copy

## What Gets Applied Automatically
- GitHub Release draft creation uses `dist/release-notes.md` automatically as the release body.
- ZIP, MSI, and MSIX artifacts are attached to the draft release automatically.

## What Still Requires Manual Store Entry
The current Microsoft Store workflow submits binaries, but it does not update Store listing text fields such as description, keywords, screenshots, or the "What's new" field.

Use the generated files below when updating Partner Center manually:
- `dist/store-listing-ja.md`
- `dist/store-listing-en.md`
- `dist/store-whats-new-ja.md`
- `dist/store-whats-new-en.md`

## Microsoft Store Submission Automation
For an existing live free Microsoft Store product, pushing a tag in the format `vX.Y.Z` triggers [release-to-store.yml](../.github/workflows/release-to-store.yml).

Behavior:
1. The workflow validates the tag format.
2. The three-part tag version is normalized to a four-part Store manifest version.
3. The Windows Application Packaging Project is built as a Store upload package.
4. The `.msixupload` package is uploaded through the Microsoft Store CLI as a draft submission.
5. The workflow commits that draft submission as a separate step.

Example:
- `v0.4.2` becomes `0.4.2.0` inside the Store packaging workflow.

Four-component tags are not accepted for Store submissions.

### Store Workflow Success Criteria
The Store workflow is intentionally limited to operations that directly control submission:

- create the Store upload package
- create or update the draft submission
- commit the draft submission

Submission status polling is not part of the workflow success criteria.

Reasoning:
- The Microsoft Store ingestion APIs can return transient errors while status polling even after submission commit has already started.
- A release workflow should fail when submission creation or submission commit fails.
- A release workflow should not fail solely because post-submit status observation is temporarily unavailable.

If you need to confirm the later Partner Center state, verify it separately in Partner Center or with a follow-up manual check.

## Store Workflow Prerequisites
GitHub secrets:
- `AZURE_AD_TENANT_ID`
- `AZURE_AD_APPLICATION_CLIENT_ID`
- `AZURE_AD_APPLICATION_SECRET`
- `SELLER_ID`

GitHub variable:
- `MSSTORE_PRODUCT_ID`

The Azure AD app registration must already be authorized in Partner Center for the seller account.

## Recommended Release Checklist
1. Add `packaging/release-notes-<version>.md` or the localized `release-notes-ja/en-<version>.md` files if the release has user-visible changes.
2. Create and push `vX.Y.Z`.
3. Review the generated GitHub draft release body.
4. Publish the GitHub Release after checking attached ZIP, MSI, and MSIX artifacts.
5. Confirm that the Store workflow created and committed the submission; if later status details are needed, verify them in Partner Center.
6. If you are updating the Microsoft Store listing text, copy from the generated Store metadata files.

## Local Test Command
You can generate the metadata locally before tagging.

```powershell
.\packaging\generate-release-metadata.ps1 -Version v0.2.3 -OutputDir dist-test
```
