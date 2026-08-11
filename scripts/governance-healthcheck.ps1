<#
.SYNOPSIS
    TempestOS Governance Health-Check Tool (WP 11.2A).

.DESCRIPTION
    Read-only validation of governance-document consistency: cross-checks
    registers, indexes, and release folders against what the repository
    actually contains, so drift is caught by a machine on every push
    rather than by the next Work Package's own manual audit — the
    recurring pattern FCR-0005 (Governance Register Health-Check Tooling)
    has named across six independent prior instances, and WP11.0A found
    a seventh (see docs/releases/v0.11.0/WP11.2A Governance Health-Check
    Tool.md for the full account).

    This script never writes to the repository it scans. Every check
    reads files under -RepoRoot and reports findings to the console and
    (optionally) a plain-text summary file OUTSIDE the repository tree.
    Findings are reported, never corrected — no check offers or accepts
    a "-Fix" mode.

.PARAMETER RepoRoot
    The repository root to validate. Defaults to this script's own
    parent directory (mirrors scripts/new-release.ps1's convention).
    Overridable so this tool can be pointed at an isolated copy of the
    repository for its own testing (see "Verification" in the
    accompanying Work Package document) without ever touching the real,
    tracked repository.

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

# Extracts backtick-quoted, path-shaped spans from free-text governance
# prose (e.g. `docs/releases/v0.10.0/Release Notes.md`) - deliberately
# scoped to unambiguous references only: must contain "/" and either end
# in a recognised extension or end in "/" (a directory reference). A bare
# filename in backticks (`ADR Register.md`, `WP 10.9A`) is NOT a path
# this repository's own convention makes resolvable without more context
# (which sibling folder is it relative to?) and is deliberately excluded
# rather than guessed at - a real, disclosed scope limit, not a silent gap.
#
# Span length is bounded (200 chars) and content is whitespace-normalised
# before validation: this project's own prose hard-wraps at ~72-80
# columns, so a single genuine inline code span routinely straddles a
# line break in the raw file - found directly, by this function crashing
# on an unbounded, un-normalised version of itself during this Work
# Package's own development (see WP11.2A Governance Health-Check Tool.md,
# "Evidence & Findings"). The strict `[\w .\-\/]` character class (no
# punctuation, no em-dashes, no parentheses) is what actually excludes
# ordinary prose that merely happens to contain a "/" - not the length
# bound alone.
function Get-PathLikeReferences
{
    param([Parameter(Mandatory)][string]$Text)

    $spanPattern = '`([^`]{1,200}?)`'
    $rawSpans = [regex]::Matches($Text, $spanPattern) | ForEach-Object { $_.Groups[1].Value }

    $candidates = foreach ($span in $rawSpans)
    {
        $normalised = ($span -replace '\s+', ' ').Trim()

        $looksLikePath = ($normalised -match '^[\w][\w .\-]*(\/[\w][\w .\-]*)*\/?$') -and ($normalised -match '\/')
        $looksLikeFile = $looksLikePath -and ($normalised -match '\.(md|cs|csproj|json|yml|yaml|ps1)$')
        $looksLikeDir  = $looksLikePath -and $normalised.EndsWith('/')

        # A shell-command example in prose ("dotnet build tests/…") is
        # technically punctuation-free and slips past the check above -
        # excluded here by requiring the candidate's own first path
        # segment to be a real top-level entry of this repository, or the
        # numbered Academy-category shorthand ("02 Runtime Architecture/…")
        # this project's own prose uses when the `docs/academy/` prefix is
        # already established by context - kept, not dropped, since it is
        # a real (if ambiguous) reference, not noise.
        $firstSegment = ($normalised -split '/')[0]
        $knownRoots = @("docs", "src", "tests", "scripts", ".github")
        $isKnownRoot = ($knownRoots -contains $firstSegment) -or ($firstSegment -match '^\d\d[A-Za-z ]*$')

        if (($looksLikeFile -or $looksLikeDir) -and $isKnownRoot)
        {
            $normalised
        }
    }

    $candidates | Sort-Object -Unique
}

# Returns the repository's real "vX.Y.Z" git tags, or an empty array if
# git is not installed, -Root is not a git repository, or the command
# otherwise fails - never throws. Centralised here because three checks
# need it and modern PowerShell (with
# $PSNativeCommandUseErrorActionPreference on by default) promotes a
# non-zero native-command exit code to a terminating exception under
# $ErrorActionPreference = "Stop" - found directly, during this Work
# Package's own failure-detection testing against an isolated,
# deliberately non-git fixture (see WP11.2A Governance Health-Check
# Tool.md, "Evidence & Findings"): every earlier, per-check-duplicated
# guard against this missed at least one call site.
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

function Test-RepoPath
{
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$Root
    )

    # Defensive: Get-PathLikeReferences already validates shape strictly,
    # but a candidate resolving to an invalid filesystem path is treated
    # as "does not exist," never a tool crash - one malformed reference
    # must never take the whole health check down (deterministic, CI-safe
    # per this Work Package's own Engineering Requirements).
    try
    {
        $candidate = Join-Path $Root ($RelativePath -replace '/', [System.IO.Path]::DirectorySeparatorChar)
        Test-Path -LiteralPath $candidate
    }
    catch
    {
        $false
    }
}

# Extracts every markdown link target ending in .md from an index-style
# document, URL-decodes it, and resolves it relative to that document's
# own directory - the same resolution rule a browser/GitHub applies to a
# relative markdown link.
function Get-ResolvedMarkdownLinks
{
    param(
        [Parameter(Mandatory)][string]$IndexFilePath
    )

    $text = Get-Content -LiteralPath $IndexFilePath -Raw
    $indexDir = Split-Path -Parent $IndexFilePath
    $linkMatches = [regex]::Matches($text, '\]\(([^)]+\.md)\)')

    $linkMatches | ForEach-Object {
        $decoded = [System.Uri]::UnescapeDataString($_.Groups[1].Value)
        $decoded = $decoded -replace '#.*$', ''  # drop any in-page anchor
        $full = [System.IO.Path]::GetFullPath((Join-Path $indexDir $decoded))
        [pscustomobject]@{ RawLink = $_.Groups[1].Value; ResolvedPath = $full }
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
# Check 2 - Academy index matches Academy articles
# ---------------------------------------------------------------------------

function Test-AcademyIndexMatchesArticles
{
    param([Parameter(Mandatory)][string]$Root)

    $indexPath = Join-Path $Root "docs\academy\Academy Index.md"
    $academyDir = Join-Path $Root "docs\academy"

    if (!(Test-Path $indexPath))
    {
        return New-CheckResult -Name "Academy Index matches docs/academy/ articles" -Status Fail -Details @("Academy Index.md not found at '$indexPath'.")
    }

    $links = Get-ResolvedMarkdownLinks -IndexFilePath $indexPath
    $brokenLinks = $links | Where-Object { -not (Test-Path -LiteralPath $_.ResolvedPath) }

    $realArticles = Get-ChildItem -LiteralPath $academyDir -Recurse -Filter "*.md" |
        Where-Object { $_.FullName -ne $indexPath }

    $linkedPaths = $links.ResolvedPath | ForEach-Object { $_.ToLowerInvariant() } | Sort-Object -Unique
    $orphanedArticles = $realArticles | Where-Object { $linkedPaths -notcontains $_.FullName.ToLowerInvariant() }

    $details = @()
    if ($brokenLinks) { $details += "Broken link(s) in Academy Index.md (target does not exist): $($brokenLinks.RawLink -join ', ')" }
    if ($orphanedArticles) { $details += "Article(s) under docs/academy/ not linked from Academy Index.md: $($orphanedArticles.FullName | ForEach-Object { $_.Substring($Root.Length + 1) } | Sort-Object)" -join '; ' }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Academy Index matches docs/academy/ articles ($(@($realArticles).Count) files, $(@($links).Count) links)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 3 - Release Register matches actual git release tags (where available)
# ---------------------------------------------------------------------------

function Test-ReleaseRegisterMatchesTags
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Delivery\Release Register.md"

    if (!(Test-Path $registerPath))
    {
        return New-CheckResult -Name "Release Register matches git tags" -Status Fail -Details @("Release Register.md not found at '$registerPath'.")
    }

    $tags = @(Get-RepoTags -Root $Root | Sort-Object -Unique)

    if ($tags.Count -eq 0)
    {
        return New-CheckResult -Name "Release Register matches git tags" -Status Warn -Details @("No git tags available (git missing, -Root is not a git repository, or zero 'v*' tags exist) - check skipped, per this Work Package's own 'where available' scope.")
    }

    $registerText = Get-Content -LiteralPath $registerPath -Raw
    $rowPattern = '(?m)^\|\s*(v\d+\.\d+\.\d+)\s*\|\s*([^|]*)\|'
    $rows = [regex]::Matches($registerText, $rowPattern) | ForEach-Object {
        [pscustomobject]@{ Version = $_.Groups[1].Value; Status = $_.Groups[2].Value.Trim() }
    }

    $registerVersions = $rows.Version | Sort-Object -Unique

    # Direction 1 (hard requirement): every real tag has a register row.
    $tagsWithoutRow = $tags | Where-Object { $registerVersions -notcontains $_ }

    # Direction 2: a row unambiguously claiming "Released" (bold or dated,
    # never a row that itself discloses "Unknown"/"pre-Claude" status) but
    # citing no matching tag - deliberately excludes exactly the two
    # disclosed historical exceptions (v0.1.0, v0.2.0) already named in
    # this register's own text, per this check's design (see accompanying
    # Work Package document).
    $unmatchedReleasedRows = $rows | Where-Object {
        $_.Status -match 'Released' -and $_.Status -notmatch 'Unknown|pre-Claude' -and ($tags -notcontains $_.Version)
    }

    $details = @()
    if ($tagsWithoutRow) { $details += "Git tag(s) with no Release Register row: $($tagsWithoutRow -join ', ')" }
    if ($unmatchedReleasedRows) { $details += "Release Register row(s) marked Released with no matching git tag: $($unmatchedReleasedRows.Version -join ', ')" }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Release Register matches git tags ($(@($tags).Count) tags, $(@($registerVersions).Count) register versions)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 4 - Documentation Register entries reference files that actually exist
# ---------------------------------------------------------------------------

function Test-DocumentationRegisterReferences
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Documentation\Documentation Register.md"

    if (!(Test-Path $registerPath))
    {
        return New-CheckResult -Name "Documentation Register references exist" -Status Fail -Details @("Documentation Register.md not found at '$registerPath'.")
    }

    $text = Get-Content -LiteralPath $registerPath -Raw
    $references = Get-PathLikeReferences -Text $text

    $missing = $references | Where-Object { -not (Test-RepoPath -RelativePath $_ -Root $Root) }

    $details = @()
    if ($missing) { $details += "Referenced path(s) that do not exist: $($missing -join ', ')" }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Documentation Register path references exist ($(@($references).Count) path-like references checked)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 5 - PROJECT_STATUS references valid releases and work packages
# ---------------------------------------------------------------------------

function Test-ProjectStatusReferences
{
    param([Parameter(Mandatory)][string]$Root)

    $statusPath = Join-Path $Root "PROJECT_STATUS.md"

    if (!(Test-Path $statusPath))
    {
        return New-CheckResult -Name "PROJECT_STATUS.md references are valid" -Status Fail -Details @("PROJECT_STATUS.md not found at '$statusPath'.")
    }

    $text = Get-Content -LiteralPath $statusPath -Raw

    # Version references: every "vX.Y.Z" token must be a real git tag, at
    # or after the VERSION file's own current version (this project
    # routinely and legitimately names its own near-term future releases
    # in prose - WP11.0B Architecture Roadmap.md names v0.12.0/v0.13.0/
    # v1.0.0 well before any of them exist as a tag or a VERSION bump,
    # and that is expected planning, not a defect), or one of the two
    # disclosed pre-Claude exceptions this repository's own Release
    # Register already names (v0.1.0, v0.2.0) - not re-derived here,
    # deliberately the same exception list Check 3 uses, so the two
    # checks never disagree. Only a version *older* than the current one
    # that is neither a real tag nor a disclosed exception is flagged -
    # that shape (a claimed-past release with no evidence) is the one a
    # genuine fabrication or typo would actually take.
    $versionTokens = [regex]::Matches($text, 'v\d+\.\d+\.\d+') | ForEach-Object { $_.Value } | Sort-Object -Unique

    $tags = Get-RepoTags -Root $Root

    $versionFilePath = Join-Path $Root "VERSION"
    $currentVersionText = if (Test-Path $versionFilePath) { (Get-Content -LiteralPath $versionFilePath -Raw).Trim() -replace '-rc\.\d+$', '' } else { $null }
    $currentVersion = if ($currentVersionText) { [version]$currentVersionText } else { $null }

    $knownExceptions = @("v0.1.0", "v0.2.0")

    $unknownVersions = $versionTokens | Where-Object {
        $token = $_
        $isTag = $tags -contains $token
        $isException = $knownExceptions -contains $token
        $isCurrentOrFuture = $false
        if ($currentVersion)
        {
            try { $isCurrentOrFuture = ([version]($token.TrimStart('v'))) -ge $currentVersion }
            catch { $isCurrentOrFuture = $false }
        }
        -not ($isTag -or $isException -or $isCurrentOrFuture)
    }

    # Path-like references, same heuristic and Root-relative resolution as
    # the Documentation Register check.
    $references = Get-PathLikeReferences -Text $text
    $missingPaths = $references | Where-Object { -not (Test-RepoPath -RelativePath $_ -Root $Root) }

    $details = @()
    if ($unknownVersions) { $details += "Version token(s) referenced that match no git tag, the current VERSION, or a disclosed exception: $($unknownVersions -join ', ')" }
    if ($missingPaths) { $details += "Referenced path(s) that do not exist: $($missingPaths -join ', ')" }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "PROJECT_STATUS.md references are valid ($(@($versionTokens).Count) version tokens, $(@($references).Count) path references)" -Status $status -Details $details
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
# Check 7 - Release folders contain the mandatory release documentation
# ---------------------------------------------------------------------------

function Test-ReleaseFoldersHaveMandatoryDocs
{
    param([Parameter(Mandatory)][string]$Root)

    $releasesDir = Join-Path $Root "docs\releases"
    $releaseDirs = Get-ChildItem -LiteralPath $releasesDir -Directory | Where-Object { $_.Name -match '^v\d+\.\d+\.\d+$' } | Sort-Object Name

    $tags = Get-RepoTags -Root $Root

    $failDetails = @()
    $warnDetails = @()

    foreach ($dir in $releaseDirs)
    {
        $isTagged = $tags -contains $dir.Name
        $hasReleaseNotes = (Test-Path (Join-Path $dir.FullName "Release Notes.md")) -or (Test-Path (Join-Path $dir.FullName "ReleaseNotes.md"))
        $hasWorkPackages = Test-Path (Join-Path $dir.FullName "WorkPackages.md")

        if ($isTagged -and -not $hasReleaseNotes)
        {
            # Hard requirement: a shipped (tagged) release with no release
            # notes of either naming variant is a genuine documentation gap.
            $failDetails += "$($dir.Name) is tagged but has neither 'Release Notes.md' nor 'ReleaseNotes.md'."
        }

        if (-not $hasWorkPackages)
        {
            # Informational only: v0.9.0 and v0.10.0 are both real,
            # already-released, fully-governed releases that genuinely
            # never had a WorkPackages.md - treating this as a hard
            # failure would misrepresent accepted history as a defect.
            $warnDetails += "$($dir.Name) has no WorkPackages.md (informational - not every past release has one; see WP11.2A Governance Health-Check Tool.md)."
        }
    }

    $status = if ($failDetails.Count -gt 0) { "Fail" } elseif ($warnDetails.Count -gt 0) { "Warn" } else { "Pass" }
    New-CheckResult -Name "Release folders contain mandatory documentation ($(@($releaseDirs).Count) release folders checked)" -Status $status -Details ($failDetails + $warnDetails)
}

# ---------------------------------------------------------------------------
# Check 8 - Missing or orphaned governance documents (Governance Index.md)
# ---------------------------------------------------------------------------

function Test-GovernanceIndexOrphans
{
    param([Parameter(Mandatory)][string]$Root)

    $indexPath = Join-Path $Root "docs\governance\Governance Index.md"
    $governanceDir = Join-Path $Root "docs\governance"

    if (!(Test-Path $indexPath))
    {
        return New-CheckResult -Name "Governance Index has no missing or orphaned documents" -Status Fail -Details @("Governance Index.md not found at '$indexPath'.")
    }

    $links = Get-ResolvedMarkdownLinks -IndexFilePath $indexPath
    $brokenLinks = $links | Where-Object { -not (Test-Path -LiteralPath $_.ResolvedPath) }

    $realRegisters = Get-ChildItem -LiteralPath $governanceDir -Recurse -Filter "*.md" |
        Where-Object { $_.FullName -ne $indexPath }

    $linkedPaths = $links.ResolvedPath | ForEach-Object { $_.ToLowerInvariant() } | Sort-Object -Unique
    $orphaned = $realRegisters | Where-Object { $linkedPaths -notcontains $_.FullName.ToLowerInvariant() }

    $details = @()
    if ($brokenLinks) { $details += "Broken link(s) in Governance Index.md: $($brokenLinks.RawLink -join ', ')" }
    if ($orphaned) { $details += "Document(s) under docs/governance/ not linked from Governance Index.md: $($orphaned.FullName | ForEach-Object { $_.Substring($Root.Length + 1) } | Sort-Object)" -join '; ' }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Governance Index has no missing or orphaned documents ($(@($realRegisters).Count) files, $(@($links).Count) links)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Run all checks, in a fixed, deterministic order
# ---------------------------------------------------------------------------

$checks = @(
    { Test-AdrRegisterMatchesFiles -Root $RepoRoot },
    { Test-AcademyIndexMatchesArticles -Root $RepoRoot },
    { Test-ReleaseRegisterMatchesTags -Root $RepoRoot },
    { Test-DocumentationRegisterReferences -Root $RepoRoot },
    { Test-ProjectStatusReferences -Root $RepoRoot },
    { Test-VersionMatchesPlannedRelease -Root $RepoRoot },
    { Test-ReleaseFoldersHaveMandatoryDocs -Root $RepoRoot },
    { Test-GovernanceIndexOrphans -Root $RepoRoot }
)

$results = @()

foreach ($check in $checks)
{
    try
    {
        $results += (& $check)
    }
    catch
    {
        # A check that throws is itself a Fail, not a silent skip - a
        # crashing check reports exactly as much uncertainty as any other
        # failure, per this tool's own "report, never hide" purpose.
        $results += New-CheckResult -Name "Unhandled error in a governance check" -Status Fail -Details @($_.Exception.Message)
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
# found directly, during this Work Package's own testing, as an empty
# "warned" figure in the summary line rather than "1". @(...) forces
# array semantics unconditionally, for 0, 1, or many matches alike.
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
