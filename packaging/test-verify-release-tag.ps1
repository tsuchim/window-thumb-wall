[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$verifier = Join-Path $PSScriptRoot 'verify-release-tag.ps1'
$tempRoot = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))
$scratch = Join-Path $tempRoot ('window-thumb-wall-tag-fixture-' + [guid]::NewGuid().ToString('N'))
$repo = Join-Path $scratch 'repo'
$trustedKey = Join-Path $scratch 'trusted'
$untrustedKey = Join-Path $scratch 'untrusted'
$signers = Join-Path $scratch 'allowed-signers'
$principal = 'fixture@example.invalid'

function Invoke-FixtureGit {
    param(
        [Parameter(Mandatory = $true)] [string[]] $GitArguments,
        [int[]] $AllowedExitCodes = @(0)
    )
    $result = @(& git -C $script:repo @GitArguments 2>&1)
    $exitCode = $LASTEXITCODE
    if ($AllowedExitCodes -notcontains $exitCode) {
        $detail = ($result | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
        throw "Fixture Git command failed (exit $exitCode): $detail"
    }
    return ($result | ForEach-Object { [string] $_ }) -join [Environment]::NewLine
}

function Assert-Failure {
    param(
        [Parameter(Mandatory = $true)] [string] $Name,
        [Parameter(Mandatory = $true)] [string] $ExpectedText,
        [Parameter(Mandatory = $true)] [hashtable] $Arguments
    )
    try {
        Push-Location $script:repo
        try {
            $null = & $script:verifier @Arguments
        }
        finally {
            Pop-Location
        }
    }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedText*") {
            throw "Fixture '$Name' failed for an unexpected reason: $($_.Exception.Message)"
        }
        Write-Output "PASS reject $Name"
        return
    }
    throw "Fixture '$Name' unexpectedly passed."
}

try {
    New-Item -ItemType Directory -Path $repo -Force | Out-Null
    foreach ($keyPath in @($trustedKey, $untrustedKey)) {
        $keygenOutput = @(& ssh-keygen -q -t ed25519 -N '' -C $principal -f $keyPath 2>&1)
        $keygenExit = $LASTEXITCODE
        if ($keygenExit -ne 0) {
            throw "ssh-keygen failed (exit $keygenExit): $($keygenOutput -join ' ')"
        }
    }

    $trustedPublic = (Get-Content -LiteralPath ($trustedKey + '.pub') -Raw).Trim() -split '\s+'
    Set-Content -LiteralPath $signers -Value "$principal namespaces=`"git`" $($trustedPublic[0]) $($trustedPublic[1])" -NoNewline

    $null = Invoke-FixtureGit -GitArguments @('init', '-b', 'main')
    $null = Invoke-FixtureGit -GitArguments @('config', 'user.name', 'Release Fixture')
    $null = Invoke-FixtureGit -GitArguments @('config', 'user.email', $principal)
    $null = Invoke-FixtureGit -GitArguments @('config', 'gpg.format', 'ssh')
    $null = Invoke-FixtureGit -GitArguments @('config', 'user.signingkey', $trustedKey)
    $null = Invoke-FixtureGit -GitArguments @('config', 'commit.gpgsign', 'false')
    $null = Invoke-FixtureGit -GitArguments @('config', 'tag.gpgSign', 'false')
    Set-Content -LiteralPath (Join-Path $repo 'fixture.txt') -Value 'fixture commit' -NoNewline
    $null = Invoke-FixtureGit -GitArguments @('add', 'fixture.txt')
    $null = Invoke-FixtureGit -GitArguments @('commit', '-m', 'fixture commit')
    $commit = (Invoke-FixtureGit -GitArguments @('rev-parse', 'HEAD')).Trim()
    $tree = (Invoke-FixtureGit -GitArguments @('rev-parse', 'HEAD^{tree}')).Trim()
    $null = Invoke-FixtureGit -GitArguments @('update-ref', 'refs/remotes/origin/main', $commit)

    $null = Invoke-FixtureGit -GitArguments @('tag', '-s', '-m', 'signed fixture', 'v1.2.3', $commit)
    $tagObject = (Invoke-FixtureGit -GitArguments @('show-ref', '--verify', '--hash', 'refs/tags/v1.2.3')).Trim()
    $positiveArgs = @{
        TagRef = 'refs/tags/v1.2.3'
        ExpectedRevision = $tagObject
        ExpectedTagObject = $tagObject
        ExpectedTree = $tree
        AllowedSignersPath = $signers
    }
    Push-Location $repo
    try {
        $verified = & $verifier @positiveArgs
    }
    finally {
        Pop-Location
    }
    if ($verified.TagObject -cne $tagObject -or $verified.Commit -cne $commit -or $verified.Tree -cne $tree) {
        throw 'Positive fixture returned unexpected tag, commit, or tree values.'
    }
    Write-Output 'PASS verify trusted signed tag'

    $badRefArgs = $positiveArgs.Clone()
    $badRefArgs.TagRef = 'refs/tags/v01.2.3'
    Assert-Failure -Name 'malformed semantic ref' -ExpectedText 'fully qualified semantic version tag ref' -Arguments $badRefArgs

    $null = Invoke-FixtureGit -GitArguments @('tag', 'v1.2.4', $commit)
    $lightweightArgs = $positiveArgs.Clone()
    $lightweightArgs.Remove('ExpectedTagObject')
    $lightweightArgs.TagRef = 'refs/tags/v1.2.4'
    $lightweightArgs.ExpectedRevision = (Invoke-FixtureGit -GitArguments @('show-ref', '--verify', '--hash', 'refs/tags/v1.2.4')).Trim()
    Assert-Failure -Name 'lightweight tag' -ExpectedText 'not an annotated tag object' -Arguments $lightweightArgs

    $null = Invoke-FixtureGit -GitArguments @('tag', '-a', '-m', 'unsigned fixture', 'v1.2.5', $commit)
    $unsignedArgs = $positiveArgs.Clone()
    $unsignedArgs.Remove('ExpectedTagObject')
    $unsignedArgs.TagRef = 'refs/tags/v1.2.5'
    $unsignedArgs.ExpectedRevision = (Invoke-FixtureGit -GitArguments @('show-ref', '--verify', '--hash', 'refs/tags/v1.2.5')).Trim()
    Assert-Failure -Name 'unsigned annotated tag' -ExpectedText 'must contain one SSH signature' -Arguments $unsignedArgs

    $null = Invoke-FixtureGit -GitArguments @('config', 'user.signingkey', $untrustedKey)
    $null = Invoke-FixtureGit -GitArguments @('tag', '-s', '-m', 'untrusted fixture', 'v1.2.6', $commit)
    $untrustedArgs = $positiveArgs.Clone()
    $untrustedArgs.Remove('ExpectedTagObject')
    $untrustedArgs.TagRef = 'refs/tags/v1.2.6'
    $untrustedArgs.ExpectedRevision = (Invoke-FixtureGit -GitArguments @('show-ref', '--verify', '--hash', 'refs/tags/v1.2.6')).Trim()
    Assert-Failure -Name 'untrusted signer' -ExpectedText 'Git command failed' -Arguments $untrustedArgs

    $mismatchArgs = $positiveArgs.Clone()
    $mismatchArgs.ExpectedRevision = ('f' * $tagObject.Length)
    Assert-Failure -Name 'event revision mismatch' -ExpectedText 'matches neither the raw tag object' -Arguments $mismatchArgs

    $wrongTreeArgs = $positiveArgs.Clone()
    $wrongTreeArgs.ExpectedTree = ('f' * $tree.Length)
    Assert-Failure -Name 'expected tree mismatch' -ExpectedText 'does not match ExpectedTree' -Arguments $wrongTreeArgs

    $wrongObjectArgs = $positiveArgs.Clone()
    $wrongObjectArgs.ExpectedTagObject = ('f' * $tagObject.Length)
    Assert-Failure -Name 'frozen tag object mismatch' -ExpectedText 'does not match ExpectedTagObject' -Arguments $wrongObjectArgs

    $null = Invoke-FixtureGit -GitArguments @('update-ref', 'refs/tags/v1.2.7', $tagObject)
    $aliasArgs = $positiveArgs.Clone()
    $aliasArgs.TagRef = 'refs/tags/v1.2.7'
    Assert-Failure -Name 'tag ref alias' -ExpectedText 'embedded annotated tag name does not match' -Arguments $aliasArgs

    Set-Content -LiteralPath (Join-Path $repo 'wrong-checkout.txt') -Value 'wrong checkout' -NoNewline
    $null = Invoke-FixtureGit -GitArguments @('add', 'wrong-checkout.txt')
    $null = Invoke-FixtureGit -GitArguments @('commit', '-m', 'wrong checkout')
    Assert-Failure -Name 'wrong checkout' -ExpectedText 'HEAD does not equal' -Arguments $positiveArgs

    $null = Invoke-FixtureGit -GitArguments @('config', 'user.signingkey', $trustedKey)
    $null = Invoke-FixtureGit -GitArguments @('tag', '-s', '-m', 'outside main', 'v1.2.8', 'HEAD')
    $outOfMainArgs = @{
        TagRef = 'refs/tags/v1.2.8'
        ExpectedRevision = (Invoke-FixtureGit -GitArguments @('rev-parse', 'HEAD')).Trim()
        AllowedSignersPath = $signers
    }
    Assert-Failure -Name 'signed commit outside main' -ExpectedText 'not reachable from refs/remotes/origin/main' -Arguments $outOfMainArgs

    Write-Output 'PASS all release tag verifier fixtures'
}
finally {
    if (Test-Path -LiteralPath $scratch -PathType Container) {
        $resolvedScratch = (Resolve-Path -LiteralPath $scratch).Path
        if ([IO.Path]::GetDirectoryName($resolvedScratch) -ne $tempRoot -or
            [IO.Path]::GetFileName($resolvedScratch) -notmatch '^window-thumb-wall-tag-fixture-[a-f0-9]{32}$') {
            throw 'Refused cleanup outside the fixture directory.'
        }
        Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
    }
}
