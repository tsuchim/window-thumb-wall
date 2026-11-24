# Release Process

WindowThumbWall uses a tag-driven release flow. Pushing a version tag builds all three official package formats and generates release metadata for GitHub Releases and Microsoft Store copy.

## Release Gate

A normal push of a new `vX.Y.Z` tag is the release trigger. GitHub Actions verifies
the annotated tag against the SSH signer list from protected main and binds the
tag object to its commit and source tree. Windows tests, distribution builds and
MSI lifecycle verification precede the Store operation. The Store job verifies
its package and the tag again before submission. The draft GitHub Release
requires the same successful run and verifies the release provenance and all
downloaded package hashes before attaching CI assets. A manual build dispatch
is for verification and does not create a draft or submit to the Store.

The release run retains its provenance as a workflow artifact. It records the
exact tag object, commit, tree, toolchain, and SHA-256 values and file sizes.
Review the draft and actual Store submission separately before announcing the
release. Store submission is followed by Microsoft certification.

## Privacy Pages

[Deploy Privacy Page](../.github/workflows/deploy-pages.yml) is an operational
publication workflow for the English and Japanese privacy notices. Its automatic
trigger is a main push affecting those notices or their Pages workflow. Its
manual trigger also publishes to the `github-pages` environment. Treat this as a
dedicated publication operation when changing or running it.

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
1. Create an annotated tag with the repository's trusted SSH signer.

```bash
git tag -s v0.2.3 -m "Release v0.2.3"
```

2. Push the tag.

```bash
git push origin v0.2.3
```

## Version Alignment Map
Treat the Git tag `vX.Y.Z` as the release source of truth. Use the table below to decide which places must be updated manually and which places are derived automatically.

| Target | Format | How it is kept in sync | Notes |
| --- | --- | --- | --- |
| Git tag | `vX.Y.Z` | Manual | Release trigger. Both release workflows start from this tag format. |
| [WindowThumbWall.csproj](../WindowThumbWall.csproj) | `X.Y.Z` and `X.Y.Z.0` | Manual | Update `Version`, `AssemblyVersion`, and `FileVersion` together. |
| [packaging/AppxManifest.xml](../packaging/AppxManifest.xml) | `X.Y.Z.0` | Manual in source, automatic in release output | The build workflow patches the copied manifest inside the release package from the tag, but the source manifest should still be kept current for local packaging clarity. |
| [packaging/Package.appxmanifest](../packaging/Package.appxmanifest) | `X.Y.Z.0` | Manual in source, automatic in Store workflow | The Store workflow rewrites the manifest before building the Store package, but the checked-in file should still track the current release version. |
| [docs/notification-attention-design.md](notification-attention-design.md) | current behavior | Manual | Update whenever notification matching rules or attention visuals change. |
| Release notes input files | `packaging/release-notes-<version>.md` and localized variants | Manual | Create the files for the exact plain version such as `0.6.2`, without the `v` prefix. |
| Release highlights input files | `packaging/release-highlights-<version>.md` and localized variants | Manual | Optional, but if used they must match the same plain version as the tag. |
| Generated artifact filenames | `WindowThumbWall-vX.Y.Z-win-*.zip/msi/msix` | Automatic from tag | Produced by the build workflow. |
| Generated GitHub Release body | `dist/release-notes.md` | Automatic from tag and metadata inputs | Produced by [packaging/generate-release-metadata.ps1](../packaging/generate-release-metadata.ps1). |
| Generated Store metadata | `dist/store-listing-*.md`, `dist/store-whats-new-*.md` | Automatic from tag and metadata inputs | Produced by the metadata generator. |
| Store package version | `X.Y.Z.0` | Automatic from tag | Both release workflows normalize `vX.Y.Z` to a four-part Store/MSIX version. |
| GitHub Release title | `X.Y.Z` | Manual today | The draft release is currently created with `WindowThumbWall vX.Y.Z`, so rename it to the plain version before publishing if you want the numeric-only convention. |

### Minimum Manual Update Set
Before creating `vX.Y.Z`, make sure these version-bearing sources have been reviewed together:

1. [WindowThumbWall.csproj](../WindowThumbWall.csproj)
2. [packaging/AppxManifest.xml](../packaging/AppxManifest.xml)
3. [packaging/Package.appxmanifest](../packaging/Package.appxmanifest)
4. [docs/notification-attention-design.md](notification-attention-design.md) when notification matching or attention cues changed
5. `packaging/release-notes-<version>.md` or localized equivalents when the release has user-visible changes
6. `packaging/release-highlights-<version>.md` or localized equivalents if you use highlights files

After the draft GitHub Release is created, also verify that the release title follows the chosen convention.

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
For an existing live free Microsoft Store product, the signed-tag build calls
[release-to-store.yml](../.github/workflows/release-to-store.yml) only after the
required tests and distribution builds succeed.

Behavior:
1. The workflow checks out the frozen commit and verifies the source tree.
2. The three-part tag version is normalized to a four-part Store manifest version.
3. The Windows Application Packaging Project is built as a Store upload package.
4. The workflow verifies the bundle version and architectures, then records the
   package SHA-256. It checks the frozen tag again before uploading the package
   as a draft submission through the Microsoft Store CLI.
5. The workflow rechecks the tag before committing the draft submission.
   The GitHub draft job depends on this successful Store submission.

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

If an earlier run started creating or committing a Store submission, reconcile
that submission in Partner Center before retrying. A draft GitHub Release
failure after a successful Store job should be recovered from the failed
GitHub job, with the same tag and package hashes.

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
1. Update [WindowThumbWall.csproj](../WindowThumbWall.csproj) `Version`, `AssemblyVersion`, and `FileVersion`.
2. Update [packaging/AppxManifest.xml](../packaging/AppxManifest.xml) and [packaging/Package.appxmanifest](../packaging/Package.appxmanifest) to the same four-part version.
3. Add `packaging/release-notes-<version>.md` or the localized `release-notes-ja/en-<version>.md` files if the release has user-visible changes.
4. Update [docs/notification-attention-design.md](notification-attention-design.md) if notification matching or visual attention behavior changed.
5. Add `packaging/release-highlights-<version>.md` or localized variants if you want explicit highlights instead of fallback content.
6. Verify the required release evidence, then create and push the signed annotated `vX.Y.Z` tag.
7. Read back the unchanged tag object, target commit, and source tree. Verify that the Store job committed its submission.
8. Review the generated GitHub draft and check all six attached packages against the CI provenance hashes.
9. Publish the verified draft. Track Store certification or availability separately in Partner Center.
10. If you are updating the Microsoft Store listing text, copy from the generated Store metadata files.
11. When packaging or notification behavior changed, run the local MSIX verification checklist in [developer-guide.md](developer-guide.md).

## Local Test Command
You can generate the metadata locally before tagging.

```powershell
.\packaging\generate-release-metadata.ps1 -Version v0.2.3 -OutputDir dist-test
```

If you use a versioned scratch directory such as `dist-release-0.6.5` for local review, treat it as disposable local output. Keep the checked-in metadata inputs under `packaging/`, and remove the generated directory after review if you do not need the snapshot anymore.
