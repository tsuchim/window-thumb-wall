<!-- GENERATED FILE - DO NOT EDIT MANUALLY -->
<!-- Source: ai-workbench/policies/git-workflow.md -->
# GitHub integration workflow

> Canonical policy: `ai-workbench/policies/git-workflow.md` and
> `ai-workbench/policies/github-operations.md`.

## Pull Requests and local validation

Pull Requests remain the integration surface for user-owned shared branches.
An authorized internal Pull Request with one coherent objective is not external
publication. An external Pull Request always requires explicit prior owner
approval.

Run and record the repository-declared complete local validation lane before
the final integration-candidate push, Pull Request creation or update, and
merge. Do not use a Pull Request as a progress report, review request, or CI
trigger. Do not request, enable, or wait for automatic review; an unsolicited
review is not a merge gate.

No Pull Request lifecycle event, review/comment/label/draft/ready event, or
ordinary branch/main/devel push may run integration CI or an equivalent hidden
check. Preserve the repository's existing merge methods, non-fast-forward
protection, and deletion protection where available; this shared adapter does
not replace repository-owned branch settings.

## Release

Release automation is opt-in and tag-only. A repository receives a signed
annotated-tag release workflow only after selecting an explicit declaration in
`ai-workbench/docs/operations/release-declarations.md`. Repositories without a
release/build purpose are release-ineligible and publish nothing.
