<#
.SYNOPSIS
    TempestOS Governance Health-Check Tool (WP 11.2A, extended WP 16.1B,
    reduced WP 17.0B).

.DESCRIPTION
    Read-only validation of governance-document consistency against what
    the repository actually contains, so drift is caught by a machine on
    every push rather than by a manual audit.

    WP 17.0B ("Governance reset", docs/releases/v1.0.0/WorkPackages.md)
    archives the governance-register suite this tool used to cross-check
    (Academy Index, Release Register, Documentation Register,
    PROJECT_STATUS.md's own path/version references, the Interface,
    Exception, Namespace, Future Capability and Academy-coverage
    registers) to archive/docs-2026-09/ as history, and reduces this
    script to the checks still worth automating on a much smaller,
    source-derived governance surface:

      1. ADR Register matches docs/adr/ (Check 1, unchanged).
      2. VERSION matches a planned release folder (Check 6, unchanged).
      3. Every *tagged* docs/releases/ release folder contains a
         Release Notes file (Check 7, relaxed to that one fact — the
         WorkPackages.md sub-check is dropped along with the register
         it cross-checked against; the tag gate stays, so the release
         folder currently in progress is not flagged for a document it
         is not yet time to have).
      4. docs/adr/'s file count matches the ADR Register's own row count
         (Check 14, re-pointed at the ADR Register directly now that
         Governance Index.md, its former source, is archived).
      5. Markdown lines added must not exceed code lines added on this
         branch (new — CONTRIBUTING.md's own PR budget, enforced).

    Every other check this tool used to run is deleted, including the
    three that re-derived a register straight from src/ (Interface,
    Exception, Namespace) — that register is archived, so the check has
    nothing left to compare against.

    This script never writes to the repository it scans. Every check
    reads files under -RepoRoot and reports findings to the console and
    (optionally) a plain-text summary file OUTSIDE the repository tree.
    Findings are reported, never corrected — no check offers or accepts
    a "-Fix" mode.

.PARAMETER RepoRoot
    The repository root to validate. Defaults to this script's own
    parent directory (mirrors scripts/new-release.ps1's convention).

.PARAMETER SummaryPath
    Optional. If given, the same report this script prints to the
    console is also written to this path (e.g. for
    $env:GITHUB_STEP_SUMMARY in CI). Must not resolve inside -RepoRoot —
    this script refuses to write anywhere under the tree it is
    validating.

.OUTPUTS
    Exit code 0 — every check passed (Warn results permitted).
    Exit code 1 — at least one check failed.
#>

param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..").Path,

    [string]$SummaryPath
)

$ErrorActionPreference = "Stop"

if ($SummaryPath)
{
    # Join-Path (unlike [System.IO.Path]::Combine) does not discard the
    # parent when the child is already rooted - it concatenates
    # unconditionally. An already-absolute -SummaryPath (e.g. CI's own
    # $env:RUNNER_TEMP-based path) would otherwise double into RepoRoot
    # (e.g. "...\RepoRoot\D:\a\_temp\..."), tripping the safety check
    # below for the wrong reason. Resolve directly when already rooted;
    # only join against the current location when genuinely relative.
    $resolvedSummary = if ([System.IO.Path]::IsPathRooted($SummaryPath))
    {
        [System.IO.Path]::GetFullPath($SummaryPath)
    }
    else
    {
        [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $SummaryPath))
    }
    $resolvedRoot = [System.IO.Path]::GetFullPath($RepoRoot)

    if ($resolvedSummary.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "SummaryPath ('$resolvedSummary') resolves inside RepoRoot ('$resolvedRoot') - this tool never writes inside the repository it validates."
    }
}

# ---------------------------------------------------------------------------
# Shared helpers
# ---------------------------------------------------------------------------

function New-CheckResult
{
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet("Pass", "Warn", "Fail")][string]$Status,
        [string[]]$Details = @()
    )

    [pscustomobject]@{
        Name    = $Name
        Status  = $Status
        Details = $Details
    }
}

# Returns the repository's real "vX.Y.Z" git tags, or an empty array if
# git is not installed, -Root is not a git repository, or the command
# otherwise fails - never throws. Modern PowerShell (with
# $PSNativeCommandUseErrorActionPreference on by default) promotes a
# non-zero native-command exit code to a terminating exception under
# $ErrorActionPreference = "Stop", so this goes through try/catch rather
# than trusting $LASTEXITCODE alone.
function Get-RepoTags
{
    param([Parameter(Mandatory)][string]$Root)

    if (-not (Get-Command git -ErrorAction SilentlyContinue))
    {
        return @()
    }

    try
    {
        Push-Location $Root
        $output = git tag -l "v*" 2>$null
        if ($LASTEXITCODE -ne 0) { return @() }
        return @($output | Where-Object { $_ -match '^v\d+\.\d+\.\d+$' })
    }
    catch
    {
        return @()
    }
    finally
    {
        Pop-Location
    }
}

# ---------------------------------------------------------------------------
# Check 1 - ADR index matches the actual ADR files
# ---------------------------------------------------------------------------

function Test-AdrRegisterMatchesFiles
{
    param([Parameter(Mandatory)][string]$Root)

    $adrDir = Join-Path $Root "docs\adr"
    $registerPath = Join-Path $Root "docs\governance\Architecture\ADR Register.md"

    if (!(Test-Path $adrDir) -or !(Test-Path $registerPath))
    {
        return New-CheckResult -Name "ADR Register matches docs/adr/" -Status Fail `
            -Details @("Expected paths not found: '$adrDir' and/or '$registerPath'.")
    }

    $fileIds = Get-ChildItem -LiteralPath $adrDir -Filter "ADR-*.md" |
        ForEach-Object { [regex]::Match($_.Name, 'ADR-\d{4}').Value } |
        Where-Object { $_ } | Sort-Object -Unique

    $registerText = Get-Content -LiteralPath $registerPath -Raw
    $registerIds = [regex]::Matches($registerText, '(?m)^\|\s*(ADR-\d{4})\s*\|') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique

    $filesNotInRegister = Compare-Object $fileIds $registerIds | Where-Object { $_.SideIndicator -eq '<=' } | ForEach-Object InputObject
    $registerEntriesNoFile = Compare-Object $fileIds $registerIds | Where-Object { $_.SideIndicator -eq '=>' } | ForEach-Object InputObject

    $details = @()
    if ($filesNotInRegister) { $details += "File(s) present in docs/adr/ with no ADR Register row: $($filesNotInRegister -join ', ')" }
    if ($registerEntriesNoFile) { $details += "ADR Register row(s) with no corresponding file in docs/adr/: $($registerEntriesNoFile -join ', ')" }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "ADR Register matches docs/adr/ ($(@($fileIds).Count) files, $(@($registerIds).Count) register rows)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 6 - VERSION file matches the current planned release
# ---------------------------------------------------------------------------

function Test-VersionMatchesPlannedRelease
{
    param([Parameter(Mandatory)][string]$Root)

    $versionFilePath = Join-Path $Root "VERSION"

    if (!(Test-Path $versionFilePath))
    {
        return New-CheckResult -Name "VERSION matches a planned release folder" -Status Fail -Details @("VERSION file not found at '$versionFilePath'.")
    }

    $rawVersion = (Get-Content -LiteralPath $versionFilePath -Raw).Trim()
    $baseVersion = $rawVersion -replace '-rc\.\d+$', ''
    $releaseDir = Join-Path $Root "docs\releases\v$baseVersion"

    $details = @()
    if (!(Test-Path $releaseDir))
    {
        $details += "VERSION reads '$rawVersion' but 'docs/releases/v$baseVersion/' does not exist."
    }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "VERSION ('$rawVersion') matches a planned release folder" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 7 - Release folders contain a Release Notes file (relaxed, WP 17.0B:
# the git-tag and WorkPackages.md sub-checks are dropped)
# ---------------------------------------------------------------------------

function Test-ReleaseFoldersHaveMandatoryDocs
{
    param([Parameter(Mandatory)][string]$Root)

    $releasesDir = Join-Path $Root "docs\releases"
    $releaseDirs = Get-ChildItem -LiteralPath $releasesDir -Directory | Where-Object { $_.Name -match '^v\d+\.\d+\.\d+$' } | Sort-Object Name

    $tags = Get-RepoTags -Root $Root

    $failDetails = @()

    foreach ($dir in $releaseDirs)
    {
        $hasReleaseNotes = (Test-Path (Join-Path $dir.FullName "Release Notes.md")) -or (Test-Path (Join-Path $dir.FullName "ReleaseNotes.md"))

        # Relaxed, WP 17.0B: this check now looks at exactly one fact
        # (does a *shipped* - tagged - release folder have a Release
        # Notes file?), dropping the former WorkPackages.md informational
        # sub-check entirely. A release folder with no tag yet - the one
        # currently in progress, e.g. v1.0.0 while the programme that
        # ships it is still running - is expected to have no Release
        # Notes file and is not a finding.
        if ($tags -contains $dir.Name -and -not $hasReleaseNotes)
        {
            $failDetails += "$($dir.Name) is tagged but has neither 'Release Notes.md' nor 'ReleaseNotes.md'."
        }
    }

    $status = if ($failDetails.Count -gt 0) { "Fail" } else { "Pass" }
    New-CheckResult -Name "Release folders contain a Release Notes file ($(@($releaseDirs).Count) release folders checked)" -Status $status -Details $failDetails
}

# ---------------------------------------------------------------------------
# Check 14 - docs/adr/'s file count matches the ADR Register's own row count
# (re-pointed at the ADR Register directly, WP 17.0B - Governance Index.md,
# this check's former source, is archived)
# ---------------------------------------------------------------------------

function Test-AdrCountMatchesRegister
{
    param([Parameter(Mandatory)][string]$Root)

    $adrDir = Join-Path $Root "docs\adr"
    $registerPath = Join-Path $Root "docs\governance\Architecture\ADR Register.md"

    if (!(Test-Path $adrDir) -or !(Test-Path $registerPath))
    {
        return New-CheckResult -Name "docs/adr/ file count matches the ADR Register's row count" -Status Fail `
            -Details @("Expected paths not found: '$adrDir' and/or '$registerPath'.")
    }

    $fileCount = @(Get-ChildItem -LiteralPath $adrDir -Filter "ADR-*.md").Count

    $registerText = Get-Content -LiteralPath $registerPath -Raw
    $registerCount = @([regex]::Matches($registerText, '(?m)^\|\s*(ADR-\d{4})\s*\|') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique).Count

    $details = @()
    if ($fileCount -ne $registerCount)
    {
        $details += "docs/adr/ contains $fileCount 'ADR-*.md' file(s); the ADR Register lists $registerCount unique row(s)."
    }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "docs/adr/ file count matches the ADR Register's row count ($fileCount files, $registerCount register rows)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# New check (WP 17.0B) - Markdown lines added must not exceed code lines
# added, enforcing CONTRIBUTING.md's PR budget. Compares `git diff
# --numstat` for the current branch against its merge base with `main`.
# Skipped, with a notice, when not in a git repository, when already on
# `main`, or when there is no diff to measure.
# ---------------------------------------------------------------------------

function Test-MarkdownLinesDoNotExceedCodeLines
{
    param([Parameter(Mandatory)][string]$Root)

    $checkName = "Markdown lines added must not exceed code lines added"

    if (-not (Get-Command git -ErrorAction SilentlyContinue))
    {
        return New-CheckResult -Name $checkName -Status Warn -Details @("Skipped: git is not installed.")
    }

    try
    {
        Push-Location $Root

        # Every native git call below is wrapped in its own try/catch:
        # modern PowerShell (7.3+, $PSNativeCommandUseErrorActionPreference
        # on by default) promotes a non-zero native-command exit code to a
        # terminating exception under $ErrorActionPreference = "Stop" - and
        # "this ref/repo/merge-base does not exist" is an expected, Warn-
        # worthy outcome here, never a script-ending error.
        $insideRepo = $null
        try { $insideRepo = (git rev-parse --is-inside-work-tree 2>$null) } catch { $insideRepo = $null }
        if ($LASTEXITCODE -ne 0 -or $insideRepo -ne "true")
        {
            return New-CheckResult -Name $checkName -Status Warn -Details @("Skipped: '$Root' is not a git repository.")
        }

        $currentBranch = $null
        try { $currentBranch = (git rev-parse --abbrev-ref HEAD 2>$null).Trim() } catch { $currentBranch = $null }

        if ($currentBranch -eq "main")
        {
            return New-CheckResult -Name $checkName -Status Warn -Details @("Skipped: currently on 'main' - there is no merge base with itself to diff against.")
        }

        # A CI checkout by commit SHA (this repository's own convention,
        # see .github/workflows/ci.yml) leaves HEAD detached, so
        # rev-parse --abbrev-ref HEAD reads "HEAD", not a branch name -
        # that is not "main" either, and the merge-base lookup below
        # still resolves correctly against origin/main or main.
        $mainRef = $null
        foreach ($candidate in @("origin/main", "main"))
        {
            $verifyExitCode = 1
            try { git rev-parse --verify --quiet $candidate 2>$null | Out-Null; $verifyExitCode = $LASTEXITCODE } catch { $verifyExitCode = 1 }
            if ($verifyExitCode -eq 0) { $mainRef = $candidate; break }
        }

        if (-not $mainRef)
        {
            return New-CheckResult -Name $checkName -Status Warn -Details @("Skipped: neither 'origin/main' nor 'main' could be resolved to find a merge base.")
        }

        $mergeBase = $null
        try { $mergeBase = (git merge-base HEAD $mainRef 2>$null).Trim() } catch { $mergeBase = $null }
        if ($LASTEXITCODE -ne 0 -or -not $mergeBase)
        {
            return New-CheckResult -Name $checkName -Status Warn -Details @("Skipped: no merge base found between HEAD and '$mainRef'.")
        }

        $numstat = $null
        try { $numstat = git diff --numstat "$mergeBase" HEAD 2>$null } catch { $numstat = $null }
        if ($LASTEXITCODE -ne 0 -or -not $numstat -or @($numstat).Count -eq 0)
        {
            return New-CheckResult -Name $checkName -Status Warn -Details @("Skipped: no diff between HEAD and merge base '$mergeBase'.")
        }

        $markdownAdded = 0
        $codeAdded = 0
        $markdownDeleted = 0
        $codeDeleted = 0

        foreach ($line in $numstat)
        {
            # git diff --numstat: "<added>\t<deleted>\t<path>". A binary
            # file reports "-\t-\t<path>" - excluded from both totals,
            # since neither side is a meaningful line count for it.
            $parts = $line -split "`t"
            if (@($parts).Count -lt 3) { continue }

            $addedText = $parts[0]
            $deletedText = $parts[1]
            $path = $parts[2]

            if ($addedText -eq '-' -or $deletedText -eq '-') { continue }

            $added = [int]$addedText
            $deleted = [int]$deletedText

            if ($path -match '\.md$')
            {
                $markdownAdded += $added
                $markdownDeleted += $deleted
            }
            else
            {
                $codeAdded += $added
                $codeDeleted += $deleted
            }
        }

        $details = @()
        if ($markdownAdded -gt $codeAdded)
        {
            $details += "$markdownAdded Markdown line(s) added versus $codeAdded non-Markdown line(s) added, measured against merge base '$mergeBase' with '$mainRef'. (Markdown lines removed: $markdownDeleted; non-Markdown lines removed: $codeDeleted.)"
        }

        $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
        New-CheckResult -Name "$checkName ($markdownAdded Markdown / $codeAdded non-Markdown line(s) added since '$mainRef')" -Status $status -Details $details
    }
    finally
    {
        Pop-Location
    }
}

# ---------------------------------------------------------------------------
# Run all checks, in a fixed, deterministic order
# ---------------------------------------------------------------------------

$checks = @(
    @{ Name = "ADR Register matches docs/adr/"; Action = { Test-AdrRegisterMatchesFiles -Root $RepoRoot } },
    @{ Name = "VERSION matches a planned release folder"; Action = { Test-VersionMatchesPlannedRelease -Root $RepoRoot } },
    @{ Name = "Release folders contain a Release Notes file"; Action = { Test-ReleaseFoldersHaveMandatoryDocs -Root $RepoRoot } },
    @{ Name = "docs/adr/ file count matches the ADR Register's row count"; Action = { Test-AdrCountMatchesRegister -Root $RepoRoot } },
    @{ Name = "Markdown lines added must not exceed code lines added"; Action = { Test-MarkdownLinesDoNotExceedCodeLines -Root $RepoRoot } }
)

$results = @()

foreach ($check in $checks)
{
    try
    {
        $results += (& $check.Action)
    }
    catch
    {
        # A check that throws is itself a Fail, not a silent skip - and
        # the report must name *which* check failed and *why*, not
        # swallow both into one hard-coded label. The specific check's
        # own declared Name, the exception's own runtime type, and its
        # message are all carried into the Fail result, so a CI-gate
        # failure is diagnosable from the report alone, without log
        # archaeology - the script still exits 1, and -SummaryPath still
        # receives the identical report either way.
        $exceptionType = $_.Exception.GetType().FullName
        $results += New-CheckResult -Name "$($check.Name) - threw an exception" -Status Fail `
            -Details @("${exceptionType}: $($_.Exception.Message)")
    }
}

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------

$lines = New-Object System.Collections.Generic.List[string]

[void]$lines.Add("==========================================")
[void]$lines.Add(" TempestOS Governance Health Check")
[void]$lines.Add("==========================================")
[void]$lines.Add("")
[void]$lines.Add("Repository root: $RepoRoot")
[void]$lines.Add("")

foreach ($result in $results)
{
    $marker = switch ($result.Status)
    {
        "Pass" { "[PASS]" }
        "Warn" { "[WARN]" }
        "Fail" { "[FAIL]" }
    }

    [void]$lines.Add("$marker $($result.Name)")

    foreach ($detail in $result.Details)
    {
        [void]$lines.Add("       - $detail")
    }
}

# Wrapped in @(...): Where-Object returns a bare (non-array) object when
# exactly one result matches, and a bare PSCustomObject has no .Count -
# @(...) forces array semantics unconditionally, for 0, 1, or many
# matches alike.
$passCount = @($results | Where-Object { $_.Status -eq "Pass" }).Count
$warnCount = @($results | Where-Object { $_.Status -eq "Warn" }).Count
$failCount = @($results | Where-Object { $_.Status -eq "Fail" }).Count

[void]$lines.Add("")
[void]$lines.Add("------------------------------------------")
[void]$lines.Add("Summary: $passCount passed, $warnCount warned, $failCount failed (of $($results.Count) checks)")
[void]$lines.Add("------------------------------------------")

$report = $lines -join [Environment]::NewLine

Write-Host $report

if ($SummaryPath)
{
    Set-Content -LiteralPath $SummaryPath -Value $report
}

if ($failCount -gt 0)
{
    exit 1
}

exit 0
