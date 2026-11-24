[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $TagRef,

    [Parameter(Mandatory = $true)]
    [string] $ExpectedRevision,

    [Parameter(Mandatory = $true)]
    [string] $AllowedSignersPath,

    [Parameter(Mandatory = $false)]
    [string] $ExpectedTagObject,

    [Parameter(Mandatory = $false)]
    [string] $ExpectedTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$rootOutput = @(& git rev-parse --show-toplevel 2>&1)
$rootExitCode = $LASTEXITCODE
if ($rootExitCode -ne 0) {
    $rootDetail = ($rootOutput | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
    throw "Could not resolve the current Git repository root (exit $rootExitCode): $rootDetail"
}
$repositoryRoot = ($rootOutput | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
$repositoryRoot = $repositoryRoot.Trim()
if ([string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw 'Could not resolve the current Git repository root.'
}

$tagPattern = '^refs/tags/v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
$objectIdPattern = '^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$'
if ($TagRef -cnotmatch $tagPattern) {
    throw "TagRef must be a fully qualified semantic version tag ref: refs/tags/vMAJOR.MINOR.PATCH."
}
if ($ExpectedRevision -cnotmatch $objectIdPattern) {
    throw 'ExpectedRevision must be a full Git object ID.'
}
if ($ExpectedTagObject -and $ExpectedTagObject -cnotmatch $objectIdPattern) {
    throw 'ExpectedTagObject must be a full Git object ID when supplied.'
}
if ($ExpectedTree -and $ExpectedTree -cnotmatch $objectIdPattern) {
    throw 'ExpectedTree must be a full Git object ID when supplied.'
}

$signerItem = Get-Item -LiteralPath $AllowedSignersPath -ErrorAction Stop
if ($signerItem.PSIsContainer) {
    throw 'AllowedSignersPath must name an existing file.'
}
$signersFile = $signerItem.FullName

function Invoke-RepositoryGit {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $GitArguments,

        [int[]] $AllowedExitCodes = @(0)
    )

    $captured = @(& git -C $script:repositoryRoot @GitArguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($AllowedExitCodes -notcontains $exitCode) {
        $detail = ($captured | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
        if ([string]::IsNullOrWhiteSpace($detail)) {
            $detail = 'Git returned no diagnostic output.'
        }
        throw "Git command failed (exit $exitCode): $detail"
    }

    return ($captured | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
}

# show-ref requires the exact local ref name; no remote lookup or fetch is performed.
$tagObject = (Invoke-RepositoryGit -GitArguments @('show-ref', '--verify', '--hash', $TagRef)).Trim().ToLowerInvariant()
if ($tagObject -notmatch $objectIdPattern) {
    throw "The exact local tag ref '$TagRef' did not resolve to a full object ID."
}
if ($ExpectedTagObject -and $tagObject -cne $ExpectedTagObject.ToLowerInvariant()) {
    throw 'The tag object does not match ExpectedTagObject.'
}

$tagObjectType = (Invoke-RepositoryGit -GitArguments @('cat-file', '-t', $tagObject)).Trim()
if ($tagObjectType -cne 'tag') {
    throw "The exact local ref '$TagRef' is not an annotated tag object."
}

$tagContents = Invoke-RepositoryGit -GitArguments @('cat-file', 'tag', $tagObject)
$tagHeaders = $tagContents -split "`r?`n`r?`n", 2 | Select-Object -First 1
$targetMatch = [regex]::Match($tagHeaders, '(?m)^object ([0-9a-fA-F]{40}(?:[0-9a-fA-F]{24})?)\r?$')
$typeMatch = [regex]::Match($tagHeaders, '(?m)^type ([^\r\n]+)\r?$')
$nameMatch = [regex]::Match($tagHeaders, '(?m)^tag ([^\r\n]+)\r?$')
if (-not $targetMatch.Success -or -not $typeMatch.Success -or -not $nameMatch.Success) {
    throw 'The annotated tag object has malformed or incomplete headers.'
}
if ($nameMatch.Groups[1].Value -cne $TagRef.Substring('refs/tags/'.Length)) {
    throw 'The embedded annotated tag name does not match TagRef.'
}
if ($typeMatch.Groups[1].Value -cne 'commit') {
    throw 'The annotated tag must directly target a commit; nested tags and other objects are rejected.'
}

$commit = $targetMatch.Groups[1].Value.ToLowerInvariant()
$targetType = (Invoke-RepositoryGit -GitArguments @('cat-file', '-t', $commit)).Trim()
if ($targetType -cne 'commit') {
    throw 'The annotated tag target is not a commit object.'
}

# The triggering event may identify either the raw tag object or its direct commit target.
$normalizedRevision = $ExpectedRevision.ToLowerInvariant()
if ($normalizedRevision -cne $tagObject -and $normalizedRevision -cne $commit) {
    throw 'ExpectedRevision matches neither the raw tag object nor its direct target commit.'
}

# Git detects a tag's signature format independently of the gpg.format setting.
$signatureHeaders = [regex]::Matches($tagContents, '(?m)^-----BEGIN (SSH SIGNATURE|PGP SIGNATURE|SIGNED MESSAGE)-----\r?$')
if ($signatureHeaders.Count -ne 1 -or $signatureHeaders[0].Groups[1].Value -cne 'SSH SIGNATURE' -or
    $tagContents -notmatch '(?m)^-----END SSH SIGNATURE-----\r?\n?\z') {
    throw 'The annotated tag must contain one SSH signature.'
}

# Verify the resolved object, even if another process changes the local ref.
$null = Invoke-RepositoryGit -GitArguments @(
    '-c', 'gpg.format=ssh',
    '-c', "gpg.ssh.allowedSignersFile=$signersFile",
    'verify-tag', $tagObject
)

$head = (Invoke-RepositoryGit -GitArguments @('rev-parse', '--verify', 'HEAD')).Trim().ToLowerInvariant()
if ($head -cne $commit) {
    throw 'HEAD does not equal the commit directly targeted by the tag.'
}

$tree = (Invoke-RepositoryGit -GitArguments @('rev-parse', '--verify', "$commit^{tree}")).Trim().ToLowerInvariant()
if ($ExpectedTree -and $tree -cne $ExpectedTree.ToLowerInvariant()) {
    throw 'The tagged commit tree does not match ExpectedTree.'
}

# The protected ancestry ref must already exist locally. This script never fetches.
$null = Invoke-RepositoryGit -GitArguments @('show-ref', '--verify', '--hash', 'refs/remotes/origin/main')
$null = Invoke-RepositoryGit -GitArguments @('merge-base', '--is-ancestor', $commit, 'refs/remotes/origin/main') -AllowedExitCodes @(0, 1)
$ancestorExit = $LASTEXITCODE
if ($ancestorExit -ne 0) {
    throw 'The tagged commit is not reachable from refs/remotes/origin/main.'
}

[pscustomobject]@{
    Tag       = $TagRef
    TagObject = $tagObject
    Commit    = $commit
    Tree      = $tree
}
