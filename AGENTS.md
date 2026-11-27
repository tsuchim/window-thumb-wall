# AGENTS.md

## Project boundaries

- WindowThumbWall is a Windows desktop app built with WPF on .NET 10. It displays live DWM thumbnails of external windows; it is not a screen-capture or streaming app.
- Official distribution is ZIP, MSI, and MSIX. Tagged GitHub Actions builds are the source of release artifacts; local builds are for development and verification.
- Repository work does not authorize SSH access to, inspection of, or changes on a remote or production host. Such work requires a user instruction naming the host and intended operation.

## Scope

- Complete the current user-authorized objective and its necessary follow-ups. Keep unrelated work separate. Reuse authorization already present in the task, and request a decision only when a necessary action exceeds it.
- Read and change only what the objective needs. Do not add unrelated cleanup, packaging changes, release preparation, or generated files.
- Keep public product behavior and privacy claims consistent with the implementation. Distinguish source changes, local verification, CI publication, and runtime acceptance.
- User-facing explanations are in Japanese; internal reasoning and tool decisions are in English.

## Pull request reviews

For pull requests with a recorded automated review request, this section governs the merge decision if the [generated integration workflow](.github/git-workflow.md) differs.

- Once an automated review is requested on a pull request, wait for the reviewer to submit its result before merging. Do not use manual merge or auto-merge while the request is pending. For pull requests expected to receive automated review, enable auto-merge only after this review gate is satisfied. An empty review status, passing checks, or a timeout cannot satisfy the request.
- Read the submitted review and its inline comments. Resolve actionable findings and record why any other findings require no change. Verify that the review covers the final head commit. If the head changes after review, obtain a new automated review for the new head before merging.
- Check the current review state and final head immediately before performing the normal protected merge. If the requested review fails or is unavailable, keep the PR open and restore the review route.
- For release preparation, run the hosted release rehearsal on the exact candidate before opening its PR. Before merge, verify the final head's rehearsal and review. Follow the [release procedure](docs/releasing.md) for the separate rehearsal on protected main and the tag gate.

## External contributions

Follow the [contribution and integration policy](docs/developer-guide.md#contribution-and-integration).
Keep the contributor's original commits in the original pull request. After
the final review and verification, use a signed normal merge and verify its
signature and ancestry on `main`.

## Maintained verification commands

~~~powershell
dotnet build .\WindowThumbWall.csproj -c Release
dotnet build .\WindowThumbWall.Tests\WindowThumbWall.Tests.csproj -c Release
dotnet test .\WindowThumbWall.Tests\WindowThumbWall.Tests.csproj -c Release --no-build
~~~

The no-build test command assumes the test project has already been built. Build packages with .\packaging\build-all.ps1; MSIX-only packaging uses .\packaging\build-msix.ps1 -Configuration Release and requires Visual Studio MSBuild. See [the developer guide](docs/developer-guide.md) for packaging and interactive checks.

## Interactive verification

- Use PowerShell 7 and .\packaging\start-development.ps1 for checks that require the unpackaged app. The launcher targets the native Display 3 device and returns the process ID and isolated state path. Use that state path for evidence and cleanup; do not substitute the normal user state.
- If Display 3 is unavailable, continue checks that do not need a visible app window. Do not force focus or synthesize user input. For a check that requires activating a source window, use a deliberate, valid user action through the app and record the observed result.
- Startup, a build, or a test result alone is not runtime acceptance evidence. Report only the checks actually observed.

## State and window lifecycle

Follow [the project invariants](docs/invariants.md) for window identity, slot and rule ownership, settings preservation, and DWM registration replacement. Keep user-selected anchors distinct from live HWND/process evidence, and apply delayed UI actions to the current slot identity.
