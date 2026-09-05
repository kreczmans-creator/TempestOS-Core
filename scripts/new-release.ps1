param(
    [Parameter(Mandatory)]
    [string]$Version,

    [switch]$Push,

    # Skips the hard 'CI Gate' verification below. Deliberately awkward to
    # reach for: the only legitimate use is when 'gh' cannot query the run
    # from where you are standing and you have confirmed on GitHub yourself
    # that CI Gate concluded 'success' for the exact commit being tagged.
    # It does not make an un-green commit taggable; it moves the check off
    # the machine and onto the person, who then owns it (`WP 16.1A-R1`).
    [switch]$SkipCiCheck
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path "$PSScriptRoot\.."

Set-Location $Root

Write-Host ""
Write-Host "=========================================="
Write-Host " TempestOS Release Validation"
Write-Host "=========================================="
Write-Host ""

#
# Verify branch
#
# Engineering Governance §7.1: "A release is only cut from main, never
# from a feature branch." This check is this policy's own mechanical
# enforcement - see WP11.1B Engineering Workflow.md's own "Evidence &
# Findings" section for a real, disclosed instance where this policy was
# not followed (the v0.10.0 tag itself points to the feature branch's own
# pre-merge tip, not to main) - the tag was never moved to correct it,
# per Governance §7.4's own "never silently altered" rule, but this
# script's own unconditional check is exactly what prevents a recurrence
# when actually used.
#

$currentBranch = git branch --show-current

if ($currentBranch -ne "main")
{
    throw "Releases may only be created from 'main'."
}

#
# Verify clean repository
#

$status = git status --porcelain

if ($status)
{
    throw "Repository contains uncommitted changes."
}

#
# VERSION exists
#

if (!(Test-Path "VERSION"))
{
    throw "VERSION file missing."
}

$currentVersion = (Get-Content VERSION).Trim()

if ($currentVersion -ne $Version)
{
    throw "VERSION file ($currentVersion) does not match requested version ($Version)."
}

#
# Release notes
#
# WP11.1B: corrects a real, disclosed drift found while writing the
# Engineering Workflow document - this check previously read
# "docs/releases/v$Version.md", a path convention every release from
# v0.6.0 onward abandoned in favour of "docs/releases/v$Version/Release
# Notes.md" (a directory per release, matching this repository's own
# actual, current layout - confirmed directly, `docs/releases/v0.10.0/
# Release Notes.md`). Run against the current tree, the old check would
# have thrown "Release notes missing" for every release since v0.6.0
# despite real release notes existing - never actually exercised,
# because no release since has been cut through this script. Fixed at
# its true source, not merely disclosed, per this project's own
# established "found and fixed in place" convention.
#

$releaseNotes = "docs/releases/v$Version/Release Notes.md"

if (!(Test-Path $releaseNotes))
{
    throw "Release notes missing:`n$releaseNotes"
}

#
# Tag already exists?
#

$existingTag = git tag --list "v$Version"

if ($existingTag)
{
    throw "Tag v$Version already exists."
}

#
# CI status for this commit (best-effort - `gh` is not a required
# dependency of this script; every check below it still runs
# unconditionally). Engineering Governance §2 has required the Build
# Gate and Test Gate to be machine-verified via .github/workflows/ci.yml
# since WP 11.1A - this is that requirement's own reminder at the one
# point in the release process a human is about to tag and push.
#

# `WP 16.1A-R1` (v0.16.0 review board): this check used to print a yellow
# warning and continue, so a human who did not read it could tag and push a
# commit whose `CI Gate` was red, cancelled, or had never run. Combined with
# branch protection still being unconfigured (`TD-45`), nothing mechanically
# stopped that. It is now a hard stop. Note the specific hazard the review
# board caught live: a run superseded by a later push completes as
# `cancelled`, not `failure` — so anything short of an explicit `success`
# must block, never merely "not a failure".

$ghAvailable = Get-Command gh -ErrorAction SilentlyContinue

if ($SkipCiCheck)
{
    Write-Host "-SkipCiCheck was passed: the 'CI Gate' status for this commit was NOT verified by this script. You are asserting it is green." -ForegroundColor Yellow
}
elseif ($ghAvailable)
{
    $headCommit = git rev-parse HEAD

    Write-Host "Checking CI status for $headCommit via 'gh'..."

    $conclusion = & gh run list --commit $headCommit --workflow ci.yml --limit 1 --json conclusion --jq '.[0].conclusion'

    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not query CI status via 'gh' for $headCommit. The 'CI Gate' check must be confirmed green before tagging (Engineering Governance 7.3). Resolve the query failure, or re-run with -SkipCiCheck if you have confirmed the run on GitHub yourself."
    }

    if ($conclusion -ne 'success')
    {
        $reported = if ([string]::IsNullOrWhiteSpace($conclusion)) { '(no completed run found)' } else { $conclusion }
        throw "CI for $headCommit concluded '$reported', not 'success'. This tag will not be created. A run cancelled by a later push counts as not-green and must be re-run on the exact commit being tagged."
    }

    Write-Host "CI concluded 'success' for $headCommit." -ForegroundColor Green
}
else
{
    throw "'gh' CLI not found, so the 'CI Gate' status for this commit cannot be verified. Install 'gh', or re-run with -SkipCiCheck if you have confirmed on GitHub that CI Gate is green for the exact commit being tagged."
}

#
# Build
#
# -p:TreatWarningsAsErrors=true mirrors .github/workflows/ci.yml's own
# CI build step exactly (WP 11.1A) - the same local/CI parity that
# pipeline's own design already established, applied here so this
# script's own "Build: Passed" claim means the identical thing CI's own
# does, not a weaker local check.
#

Write-Host "Building..."

dotnet build src/TempestOS.slnx -c Release -p:TreatWarningsAsErrors=true

if ($LASTEXITCODE -ne 0)
{
    throw "Build failed."
}

#
# Tests
#

Write-Host ""

Write-Host "Running Tests..."

dotnet test src/TempestOS.slnx -c Release

if ($LASTEXITCODE -ne 0)
{
    throw "Tests failed."
}

#
# Create annotated tag
#

Write-Host ""

Write-Host "Creating Tag..."

git tag `
    -a "v$Version" `
    -m "TempestOS v$Version"

#
# Push
#

if ($Push)
{
    Write-Host ""

    Write-Host "Pushing..."

    git push origin main
    git push origin "v$Version"

    Write-Host ""
    Write-Host "Pushing the tag triggers .github/workflows/release.yml, which"
    Write-Host "re-builds, re-tests, and publishes a GitHub Release with the"
    Write-Host "Release build output attached - see WP11.1B Engineering"
    Write-Host "Workflow.md, 'Version-Tagging Workflow'."
}

Write-Host ""
Write-Host "=========================================="
Write-Host " RELEASE SUCCESSFUL"
Write-Host "=========================================="
Write-Host ""
Write-Host "Version : v$Version"
Write-Host "Tag     : Created"
Write-Host "Build   : Passed"
Write-Host "Tests   : Passed"
Write-Host ""
