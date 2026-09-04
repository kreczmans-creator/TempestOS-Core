<#
.SYNOPSIS
    TempestOS Governance Health-Check Tool (WP 11.2A, extended WP 16.1B).

.DESCRIPTION
    Read-only validation of governance-document consistency: cross-checks
    registers, indexes, and release folders against what the repository
    actually contains, so drift is caught by a machine on every push
    rather than by the next Work Package's own manual audit — the
    recurring pattern FCR-0005 (Governance Register Health-Check Tooling)
    has named across six independent prior instances, and WP11.0A found
    a seventh (see docs/releases/v0.11.0/WP11.2A Governance Health-Check
    Tool.md for the full account).

    WP 16.1B adds eight further checks, each derived directly from source
    (never from another register), closing the root cause TD-57 disclosed:
    six governance registers (Interface, Exception, DI, Namespace, Platform
    Services, Validation) had drifted from source with no automated check
    to catch it. D-021 (Proposed, WP 16.0A) names trustworthy registers as
    a v1.0 readiness precondition; TD-57 was resolved by WP 16.2A
    re-deriving those registers once — this Work Package's own job is
    making sure they cannot go silently stale again the same way. See
    docs/releases/v0.16.0/WP16.1B Health-Check Extension Report.md.

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

    # Zero tags means "unenumerable in this environment" (git missing,
    # -Root not a git repository, or a shallow checkout with no local tag
    # refs - Get-RepoTags's own contract, shared verbatim with Check 3),
    # never "zero tags genuinely exist" - this repository has shipped
    # nine real releases. Check 3 (Test-ReleaseRegisterMatchesTags)
    # already treats this as a disclosed environmental limitation, Warn,
    # not a content defect; version-token validation below did not,
    # producing a false Fail against every historical version whenever
    # tags happen to be unavailable (WP 12.9.3A, confirmed directly
    # against a real GitHub Actions shallow checkout). Path-reference
    # validation, below, does not depend on tags at all and is
    # unaffected either way - WP 12.9.3B deliberately narrows the fix to
    # only the sub-check that actually depends on tag availability,
    # rather than Check 3's own coarser "skip the whole check" shape,
    # which would also have suppressed genuine path drift whenever tags
    # happen to be unavailable.
    $tagsAvailable = $tags.Count -gt 0

    $versionFilePath = Join-Path $Root "VERSION"
    $currentVersionText = if (Test-Path $versionFilePath) { (Get-Content -LiteralPath $versionFilePath -Raw).Trim() -replace '-rc\.\d+$', '' } else { $null }
    $currentVersion = if ($currentVersionText) { [version]$currentVersionText } else { $null }

    $knownExceptions = @("v0.1.0", "v0.2.0")

    $unknownVersions = if ($tagsAvailable)
    {
        $versionTokens | Where-Object {
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
    }
    else
    {
        @()
    }

    # Path-like references, same heuristic and Root-relative resolution as
    # the Documentation Register check - deliberately independent of tag
    # availability, so this sub-check runs, and can still fail the check
    # on its own, regardless of the branch above.
    $references = Get-PathLikeReferences -Text $text
    $missingPaths = $references | Where-Object { -not (Test-RepoPath -RelativePath $_ -Root $Root) }

    $details = @()
    if (-not $tagsAvailable)
    {
        $details += "Version token validation skipped: no git tags available (git missing, -Root is not a git repository, or zero 'v*' tags exist) - the identical, disclosed limitation Check 3 already names."
    }
    if ($unknownVersions) { $details += "Version token(s) referenced that match no git tag, the current VERSION, or a disclosed exception: $($unknownVersions -join ', ')" }
    if ($missingPaths) { $details += "Referenced path(s) that do not exist: $($missingPaths -join ', ')" }

    $status = if ($unknownVersions -or $missingPaths) { "Fail" }
              elseif (-not $tagsAvailable) { "Warn" }
              else { "Pass" }
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
# Check 9 (WP 16.1B) - Interface Register vs public interface declarations
# ---------------------------------------------------------------------------

function Test-InterfaceRegisterMatchesSource
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Engineering\Interface Register.md"
    $sourceDir = Join-Path $Root "src\Tempest.Core"

    if (!(Test-Path $registerPath) -or !(Test-Path $sourceDir))
    {
        return New-CheckResult -Name "Interface Register matches src/Tempest.Core/ public interfaces" -Status Fail `
            -Details @("Expected paths not found: '$registerPath' and/or '$sourceDir'.")
    }

    # Source of truth: every anchored "public interface Name<...>"
    # declaration line under src/Tempest.Core/ - the identical pattern the
    # register's own "Source of Truth" field documents deriving itself from
    # (WP 16.2A). Generic arity is stripped (ICommandHandler<T> ->
    # ICommandHandler) per this check's own "treat generic arity by name"
    # scope; the register's own Entries table lists interfaces the same way.
    $csFiles = Get-ChildItem -LiteralPath $sourceDir -Recurse -Filter "*.cs"
    $sourceNames = foreach ($file in $csFiles)
    {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        [regex]::Matches($text, '(?m)^public interface (\w+)') | ForEach-Object { $_.Groups[1].Value }
    }
    $sourceNames = @($sourceNames | Sort-Object -Unique)

    $registerText = Get-Content -LiteralPath $registerPath -Raw
    $entriesStart = $registerText.IndexOf("`n## Entries")
    $summaryStart = $registerText.IndexOf("`n## Classification Summary")
    if ($entriesStart -lt 0 -or $summaryStart -lt 0 -or $summaryStart -le $entriesStart)
    {
        return New-CheckResult -Name "Interface Register matches src/Tempest.Core/ public interfaces" -Status Fail `
            -Details @("Could not locate the 'Entries'/'Classification Summary' section boundaries in '$registerPath' - register structure has changed.")
    }
    $entriesBody = $registerText.Substring($entriesStart, $summaryStart - $entriesStart)

    $registerNames = [regex]::Matches($entriesBody, '(?m)^\|\s*`([^`]+)`\s*\|') | ForEach-Object {
        $_.Groups[1].Value -replace '<.*?>', ''
    }
    $registerNames = @($registerNames | Sort-Object -Unique)

    $missingFromRegister = Compare-Object $sourceNames $registerNames | Where-Object { $_.SideIndicator -eq '<=' } | ForEach-Object InputObject
    $staleInRegister = Compare-Object $sourceNames $registerNames | Where-Object { $_.SideIndicator -eq '=>' } | ForEach-Object InputObject

    $details = @()
    if ($missingFromRegister) { $details += "Interface(s) declared under src/Tempest.Core/ with no Interface Register row: $($missingFromRegister -join ', ')" }
    if ($staleInRegister) { $details += "Interface Register row(s) with no matching 'public interface' declaration under src/Tempest.Core/: $($staleInRegister -join ', ')" }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Interface Register matches src/Tempest.Core/ public interfaces ($(@($sourceNames).Count) declared, $(@($registerNames).Count) register rows)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 10 (WP 16.1B) - Exception Register vs classes deriving from Exception
# ---------------------------------------------------------------------------

function Test-ExceptionRegisterMatchesSource
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Engineering\Exception Register.md"
    $sourceDir = Join-Path $Root "src\Tempest.Core"

    if (!(Test-Path $registerPath) -or !(Test-Path $sourceDir))
    {
        return New-CheckResult -Name "Exception Register matches src/Tempest.Core/ exception classes" -Status Fail `
            -Details @("Expected paths not found: '$registerPath' and/or '$sourceDir'.")
    }

    # Source of truth: every anchored, top-level "public [sealed|abstract]
    # class NameException" declaration under src/Tempest.Core/ - the exact
    # pattern the register's own "Last Reviewed" field documents deriving
    # itself from (WP 16.2A).
    $csFiles = Get-ChildItem -LiteralPath $sourceDir -Recurse -Filter "*.cs"
    $sourceNames = foreach ($file in $csFiles)
    {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        [regex]::Matches($text, '(?m)^public (?:sealed |abstract )?class (\w+Exception)\b') | ForEach-Object { $_.Groups[1].Value }
    }
    $sourceNames = @($sourceNames | Sort-Object -Unique)

    $registerText = Get-Content -LiteralPath $registerPath -Raw
    $entriesStart = $registerText.IndexOf("`n## Entries")
    $noteStart = $registerText.IndexOf("`n## A Note on Background Services")
    if ($entriesStart -lt 0 -or $noteStart -lt 0 -or $noteStart -le $entriesStart)
    {
        return New-CheckResult -Name "Exception Register matches src/Tempest.Core/ exception classes" -Status Fail `
            -Details @("Could not locate the 'Entries' section boundary in '$registerPath' - register structure has changed.")
    }
    $entriesBody = $registerText.Substring($entriesStart, $noteStart - $entriesStart)

    $registerNames = @([regex]::Matches($entriesBody, '(?m)^\|\s*`([^`]+)`\s*\|') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)

    $missingFromRegister = Compare-Object $sourceNames $registerNames | Where-Object { $_.SideIndicator -eq '<=' } | ForEach-Object InputObject
    $staleInRegister = Compare-Object $sourceNames $registerNames | Where-Object { $_.SideIndicator -eq '=>' } | ForEach-Object InputObject

    $details = @()
    if ($missingFromRegister) { $details += "Exception class(es) declared under src/Tempest.Core/ with no Exception Register row: $($missingFromRegister -join ', ')" }
    if ($staleInRegister) { $details += "Exception Register row(s) with no matching class declaration under src/Tempest.Core/: $($staleInRegister -join ', ')" }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Exception Register matches src/Tempest.Core/ exception classes ($(@($sourceNames).Count) declared, $(@($registerNames).Count) register rows)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 11 (WP 16.1B) - Namespace Register vs namespace declarations
# ---------------------------------------------------------------------------

function Test-NamespaceRegisterMatchesSource
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Engineering\Namespace Register.md"

    # The register's own declared Scope (its Register Metadata "Scope"
    # row): every namespace under src/Tempest.Core/, src/Tempest.App/,
    # src/Samples/Tempest.Samples/, and, since WP 12.3B,
    # src/Validation/Tempest.Validation/ - src/Tempest.Desktop/ is
    # explicitly, disclosedly out of scope. Mirrors the register's own
    # stated re-derivation command (WP 16.2A): grep -rhoP
    # "^namespace \K[\w.]+" src/Tempest.Core src/Tempest.App src/Samples
    # src/Validation --include=*.cs
    $scopeDirs = @("src\Tempest.Core", "src\Tempest.App", "src\Samples", "src\Validation") |
        ForEach-Object { Join-Path $Root $_ } | Where-Object { Test-Path $_ }

    if (!(Test-Path $registerPath) -or @($scopeDirs).Count -eq 0)
    {
        return New-CheckResult -Name "Namespace Register matches declared-scope source" -Status Fail `
            -Details @("Expected paths not found: '$registerPath' and/or the register's declared scope directories.")
    }

    $csFiles = foreach ($dir in $scopeDirs) { Get-ChildItem -LiteralPath $dir -Recurse -Filter "*.cs" }

    $namespaceToFiles = @{}
    foreach ($file in $csFiles)
    {
        $text = Get-Content -LiteralPath $file.FullName -Raw
        $match = [regex]::Match($text, '(?m)^namespace ([\w.]+)')
        if ($match.Success)
        {
            $ns = $match.Groups[1].Value
            if (-not $namespaceToFiles.ContainsKey($ns)) { $namespaceToFiles[$ns] = 0 }
            $namespaceToFiles[$ns] = $namespaceToFiles[$ns] + 1
        }
    }
    $sourceNames = @($namespaceToFiles.Keys | Sort-Object -Unique)

    $registerText = Get-Content -LiteralPath $registerPath -Raw
    $tableStart = $registerText.IndexOf("| Namespace | Project | File Count")
    $noteStart = $registerText.IndexOf("`n## A Note on the Four Pre-Claude Namespaces")
    if ($tableStart -lt 0 -or $noteStart -lt 0 -or $noteStart -le $tableStart)
    {
        return New-CheckResult -Name "Namespace Register matches declared-scope source" -Status Fail `
            -Details @("Could not locate the Entries table boundaries in '$registerPath' - register structure has changed.")
    }
    $tableBody = $registerText.Substring($tableStart, $noteStart - $tableStart)

    # Every real row: `Namespace` | Project | FileCount ... - a FileCount of
    # "0 - retired, ..." is a deliberately-kept historical row for a
    # namespace with zero real files today (WP-C); excluded from both the
    # name and the file-count comparison below, exactly as the register's
    # own "Total: 46 namespaces" line excludes it.
    $rowMatches = [regex]::Matches($tableBody, '(?m)^\|\s*`([^`]+)`\s*\|\s*[^|]+\|\s*(\d+)')
    $registerRows = foreach ($m in $rowMatches)
    {
        [pscustomobject]@{ Name = $m.Groups[1].Value; Count = [int]$m.Groups[2].Value }
    }
    $activeRegisterRows = @($registerRows | Where-Object { $_.Count -gt 0 })
    $registerNames = @($activeRegisterRows.Name | Sort-Object -Unique)

    $missingFromRegister = Compare-Object $sourceNames $registerNames | Where-Object { $_.SideIndicator -eq '<=' } | ForEach-Object InputObject
    $staleInRegister = Compare-Object $sourceNames $registerNames | Where-Object { $_.SideIndicator -eq '=>' } | ForEach-Object InputObject

    $failDetails = @()
    if ($missingFromRegister) { $failDetails += "Namespace(s) declared in the register's scope with no Namespace Register row: $($missingFromRegister -join ', ')" }
    if ($staleInRegister) { $failDetails += "Namespace Register row(s) with no matching namespace declaration in scope: $($staleInRegister -join ', ')" }

    # Secondary, advisory sub-check: per-namespace file counts. A count
    # drifting out of sync is a lesser defect than a namespace appearing or
    # disappearing outright - reported as Warn, never escalated to Fail.
    $warnDetails = @()
    foreach ($row in $activeRegisterRows)
    {
        if ($namespaceToFiles.ContainsKey($row.Name))
        {
            $derivedCount = $namespaceToFiles[$row.Name]
            if ($derivedCount -ne $row.Count)
            {
                $warnDetails += "Namespace Register row '$($row.Name)' states $($row.Count) file(s); derived directly (grep -rl) is $derivedCount."
            }
        }
    }

    $status = if ($failDetails.Count -gt 0) { "Fail" } elseif ($warnDetails.Count -gt 0) { "Warn" } else { "Pass" }
    New-CheckResult -Name "Namespace Register matches declared-scope source ($(@($sourceNames).Count) namespaces derived, $(@($registerNames).Count) active register rows)" -Status $status -Details ($failDetails + $warnDetails)
}

# ---------------------------------------------------------------------------
# Check 12 (WP 16.1B) - Technical Debt Register summary line vs its own rows
# ---------------------------------------------------------------------------

function Test-TechnicalDebtRegisterSummaryMatchesRows
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Quality\Technical Debt Register.md"

    if (!(Test-Path $registerPath))
    {
        return New-CheckResult -Name "Technical Debt Register summary matches its own rows" -Status Fail -Details @("Technical Debt Register.md not found at '$registerPath'.")
    }

    $text = Get-Content -LiteralPath $registerPath -Raw

    # The summary/total line: the current (topmost, most-recently-reviewed)
    # "Last Reviewed" entry's own bolded tally - structurally unique in this
    # document (every older "Previously reviewed" entry states its own bare
    # "N tracked" figure without repeating the full five-way breakdown), so
    # the FIRST match in document order is the current one.
    $summaryMatch = [regex]::Match($text,
        '(\d+)\s+tracked\s+—\s+(\d+)\s+Resolved\b[^,]*,\s*(\d+)\s+Closed,\s*(\d+)\s+Open,\s*(\d+)\s+Partially resolved,\s*(\d+)\s+Deferred')

    if (-not $summaryMatch.Success)
    {
        return New-CheckResult -Name "Technical Debt Register summary matches its own rows" -Status Fail `
            -Details @("Could not locate the summary/total line ('N tracked - N Resolved, N Closed, N Open, N Partially resolved, N Deferred') in '$registerPath'.")
    }

    $statedTotal = [int]$summaryMatch.Groups[1].Value
    $statedResolved = [int]$summaryMatch.Groups[2].Value
    $statedClosed = [int]$summaryMatch.Groups[3].Value
    $statedOpen = [int]$summaryMatch.Groups[4].Value
    $statedPartial = [int]$summaryMatch.Groups[5].Value
    $statedDeferred = [int]$summaryMatch.Groups[6].Value

    $entriesStart = $text.IndexOf("## Entries — Technical Debt")
    $tradeoffsStart = $text.IndexOf("## Entries — Disclosed, Accepted Trade-offs")
    if ($entriesStart -lt 0 -or $tradeoffsStart -lt 0 -or $tradeoffsStart -le $entriesStart)
    {
        return New-CheckResult -Name "Technical Debt Register summary matches its own rows" -Status Fail `
            -Details @("Could not locate the Technical Debt entries table boundaries in '$registerPath' - register structure has changed.")
    }
    $entriesBody = $text.Substring($entriesStart, $tradeoffsStart - $entriesStart)

    $rowLines = ($entriesBody -split "`n") | Where-Object { $_ -match '^\|\s*TD-\d+\s*\|' }

    $counts = @{ Resolved = 0; Closed = 0; Open = 0; "Partially resolved" = 0; Deferred = 0 }
    $unclassified = @()
    foreach ($line in $rowLines)
    {
        # Split on the column delimiter and take the last real cell (the
        # element before the trailing empty one a well-formed row's own
        # closing "|" leaves) - robust against the row's own preceding
        # column count varying (two rows in this table fold "Since" into
        # "Debt Item" instead of keeping it a separate column; splitting by
        # position from the end, not by a fixed column offset from the
        # start, still finds the right cell either way).
        $parts = $line -split '\|'
        $statusCell = if ($parts[-1].Trim() -eq '') { $parts[-2] } else { $parts[-1] }
        $statusText = $statusCell.Trim().TrimStart('*').Trim()

        if ($statusText.StartsWith("Partially resolved")) { $counts["Partially resolved"]++ }
        elseif ($statusText.StartsWith("Resolved")) { $counts["Resolved"]++ }
        elseif ($statusText.StartsWith("Closed")) { $counts["Closed"]++ }
        elseif ($statusText.StartsWith("Open")) { $counts["Open"]++ }
        elseif ($statusText.StartsWith("Deferred")) { $counts["Deferred"]++ }
        else { $unclassified += $line.Substring(0, [Math]::Min(60, $line.Length)) }
    }

    $derivedTotal = @($rowLines).Count

    $details = @()
    if ($unclassified.Count -gt 0)
    {
        $details += "TD row(s) whose Status cell does not start with a recognised category (Resolved/Closed/Open/Partially resolved/Deferred): $($unclassified -join ' | ')"
    }
    if ($derivedTotal -ne $statedTotal) { $details += "Summary states $statedTotal tracked; $derivedTotal 'TD-nnn' rows actually present." }
    if ($counts.Resolved -ne $statedResolved) { $details += "Summary states $statedResolved Resolved; $($counts.Resolved) rows' Status cell leads with Resolved." }
    if ($counts.Closed -ne $statedClosed) { $details += "Summary states $statedClosed Closed; $($counts.Closed) rows' Status cell leads with Closed." }
    if ($counts.Open -ne $statedOpen) { $details += "Summary states $statedOpen Open; $($counts.Open) rows' Status cell leads with Open." }
    if ($counts["Partially resolved"] -ne $statedPartial) { $details += "Summary states $statedPartial Partially resolved; $($counts["Partially resolved"]) rows' Status cell leads with Partially resolved." }
    if ($counts.Deferred -ne $statedDeferred) { $details += "Summary states $statedDeferred Deferred; $($counts.Deferred) rows' Status cell leads with Deferred." }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Technical Debt Register summary matches its own rows ($statedTotal stated, $derivedTotal rows present)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 13 (WP 16.1B) - Future Capability Register currency
# ---------------------------------------------------------------------------

function Test-FutureCapabilityRegisterCurrency
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Future Capability Register.md"

    if (!(Test-Path $registerPath))
    {
        return New-CheckResult -Name "Future Capability Register currency" -Status Fail -Details @("Future Capability Register.md not found at '$registerPath'.")
    }

    $text = Get-Content -LiteralPath $registerPath -Raw

    $entryIds = [regex]::Matches($text, '(?m)^#### FCR-(\d{4})') | ForEach-Object { [int]$_.Groups[1].Value }
    # Measure-Object's own -Maximum is typed [double], which has no "D4"
    # format specifier - cast back to [int] before formatting below.
    $highestPresent = if (@($entryIds).Count -gt 0) { [int](($entryIds | Measure-Object -Maximum).Maximum) } else { 0 }

    $rangeMatch = [regex]::Match($text, 'FCR-0001[^\d]{1,10}FCR-(\d{4})')

    $failDetails = @()
    if (-not $rangeMatch.Success)
    {
        $failDetails += "Could not locate the register's own stated 'FCR-0001-FCR-nnnn' range in '$registerPath'."
    }
    else
    {
        $statedHighest = [int]$rangeMatch.Groups[1].Value
        if ($statedHighest -ne $highestPresent)
        {
            $failDetails += "Register states its range tops out at FCR-$($rangeMatch.Groups[1].Value); the highest '#### FCR-nnnn' entry actually present is FCR-$($highestPresent.ToString('D4'))."
        }
    }

    # Advisory staleness sub-check: this register's own "Last Reviewed" date
    # should not predate the newest release folder's own WorkPackages.md -
    # a register reviewed before the most recent release's work packages
    # were even written cannot have accounted for them. Advisory, per this
    # Work Package's own scope ("Out of scope: auditing prose registers
    # that cannot be derived from source ... beyond a staleness date
    # check") - reported as Warn, never Fail.
    $warnDetails = @()
    $dateMatch = [regex]::Match($text, '\|\s*\*\*Last Reviewed\*\*\s*\|\s*(\d{4}-\d{2}-\d{2})')
    if ($dateMatch.Success)
    {
        $lastReviewed = [datetime]$dateMatch.Groups[1].Value

        $releasesDir = Join-Path $Root "docs\releases"
        $releaseDirs = Get-ChildItem -LiteralPath $releasesDir -Directory |
            Where-Object { $_.Name -match '^v\d+\.\d+\.\d+$' } |
            Sort-Object { [version]($_.Name.TrimStart('v')) } -Descending

        $newestWithWorkPackages = $releaseDirs | Where-Object { Test-Path (Join-Path $_.FullName "WorkPackages.md") } | Select-Object -First 1

        if ($newestWithWorkPackages)
        {
            $wpPath = Join-Path $newestWithWorkPackages.FullName "WorkPackages.md"
            $commitDate = $null
            try
            {
                Push-Location $Root
                $raw = git log -1 --format=%cs -- $wpPath 2>$null
                if ($LASTEXITCODE -eq 0 -and $raw) { $commitDate = [datetime]$raw.Trim() }
            }
            catch { $commitDate = $null }
            finally { Pop-Location }

            if ($commitDate -and $lastReviewed -lt $commitDate)
            {
                $warnDetails += "Register's own 'Last Reviewed' date ($($lastReviewed.ToString('yyyy-MM-dd'))) is older than '$($newestWithWorkPackages.Name)/WorkPackages.md''s own last git commit date ($($commitDate.ToString('yyyy-MM-dd'))) - advisory only."
            }
        }
    }
    else
    {
        $warnDetails += "Could not locate a 'Last Reviewed' date in '$registerPath' to check staleness against the newest release folder."
    }

    $status = if ($failDetails.Count -gt 0) { "Fail" } elseif ($warnDetails.Count -gt 0) { "Warn" } else { "Pass" }
    New-CheckResult -Name "Future Capability Register currency (highest present FCR-$($highestPresent.ToString('D4')))" -Status $status -Details ($failDetails + $warnDetails)
}

# ---------------------------------------------------------------------------
# Check 14 (WP 16.1B) - Governance Index ADR count vs docs/adr/
# ---------------------------------------------------------------------------

function Test-GovernanceIndexAdrCountMatchesFiles
{
    param([Parameter(Mandatory)][string]$Root)

    $indexPath = Join-Path $Root "docs\governance\Governance Index.md"
    $adrDir = Join-Path $Root "docs\adr"

    if (!(Test-Path $indexPath) -or !(Test-Path $adrDir))
    {
        return New-CheckResult -Name "Governance Index ADR count matches docs/adr/" -Status Fail `
            -Details @("Expected paths not found: '$indexPath' and/or '$adrDir'.")
    }

    $fileCount = @(Get-ChildItem -LiteralPath $adrDir -Filter "ADR-*.md").Count

    $text = Get-Content -LiteralPath $indexPath -Raw
    $countMatch = [regex]::Match($text, 'all (\d+) Architecture Decision Records')

    if (-not $countMatch.Success)
    {
        return New-CheckResult -Name "Governance Index ADR count matches docs/adr/" -Status Fail `
            -Details @("Could not locate a stated 'all N Architecture Decision Records' count in '$indexPath'.")
    }

    $statedCount = [int]$countMatch.Groups[1].Value

    $details = @()
    if ($statedCount -ne $fileCount)
    {
        $details += "Governance Index states $statedCount Architecture Decision Records; docs/adr/ actually contains $fileCount 'ADR-*.md' file(s)."
    }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Fail" }
    New-CheckResult -Name "Governance Index ADR count matches docs/adr/ ($fileCount files, $statedCount stated)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Check 15 (WP 16.1B) - Academy Register covers every 03 Work Packages/ file
# ---------------------------------------------------------------------------

function Test-AcademyRegisterWorkPackagesCoverage
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Documentation\Academy Register.md"
    $wpDir = Join-Path $Root "docs\academy\03 Work Packages"

    if (!(Test-Path $registerPath) -or !(Test-Path $wpDir))
    {
        return New-CheckResult -Name "Academy Register covers every 03 Work Packages/ retrospective" -Status Fail `
            -Details @("Expected paths not found: '$registerPath' and/or '$wpDir'.")
    }

    $text = Get-Content -LiteralPath $registerPath -Raw
    $sectionStart = $text.IndexOf("`n## 03 Work Packages")
    $nextSectionStart = $text.IndexOf("`n## 04 Design Patterns")
    if ($sectionStart -lt 0 -or $nextSectionStart -lt 0 -or $nextSectionStart -le $sectionStart)
    {
        return New-CheckResult -Name "Academy Register covers every 03 Work Packages/ retrospective" -Status Fail `
            -Details @("Could not locate the '## 03 Work Packages' section boundaries in '$registerPath' - register structure has changed.")
    }
    $sectionBody = $text.Substring($sectionStart, $nextSectionStart - $sectionStart)

    $headerMatch = [regex]::Match($sectionBody, '## 03 Work Packages \((\d+) retrospectives\)')

    # Row identification: each row's leading cell is the retrospective's own
    # title text - the same text that file's own H1 heading carries
    # verbatim (confirmed directly against every file in this directory
    # while building this check). Two shapes coexist: a numbered
    # "WP 10.0A - ..."/"WP-A1 - ..." row, matched by its own WP identifier
    # so a later, reworded subtitle (several exist - e.g. "WP 10.3B" now
    # reads differently here than its own file's H1) does not false-fail;
    # and a handful of v0.14.0-pre-programme rows with no WP identifier at
    # all ("TD-58 - ...", "Governance - ..."), matched by an exact,
    # verbatim row/H1 title match instead, since that is the only stable
    # anchor those rows have.
    $rowLines = ($sectionBody -split "`n") | Where-Object { $_ -match '^\|' -and $_ -notmatch '^\|---' -and $_ -notmatch '^\|\s*Retrospective\s*\|' }

    $rowTitles = New-Object System.Collections.Generic.HashSet[string]
    $rowWpIds = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in $rowLines)
    {
        $cellMatch = [regex]::Match($line, '^\|\s*(.+?)\s*\|\s*.+\|\s*$')
        if (-not $cellMatch.Success) { continue }
        $title = $cellMatch.Groups[1].Value
        [void]$rowTitles.Add($title)
        $idMatch = [regex]::Match($title, '^`?WP[- ]([A-Za-z0-9.]+)`?')
        if ($idMatch.Success) { [void]$rowWpIds.Add($idMatch.Groups[1].Value) }
    }

    $files = Get-ChildItem -LiteralPath $wpDir -Filter "*.md"
    $filesWithoutRow = @()
    foreach ($file in $files)
    {
        $firstLine = Get-Content -LiteralPath $file.FullName -TotalCount 1
        $fileTitle = "$firstLine".TrimStart('#').Trim()

        $matched = $false
        $idMatch = [regex]::Match($file.Name, '^(WP-?[A-Za-z0-9.]+)-')
        if ($idMatch.Success)
        {
            $fileId = $idMatch.Groups[1].Value -replace '^WP-?', ''
            if ($rowWpIds.Contains($fileId)) { $matched = $true }
        }
        if (-not $matched -and $rowTitles.Contains($fileTitle)) { $matched = $true }

        if (-not $matched) { $filesWithoutRow += $file.Name }
    }

    $failDetails = @()
    if ($filesWithoutRow.Count -gt 0)
    {
        $failDetails += "File(s) under docs/academy/03 Work Packages/ with no Academy Register row: $($filesWithoutRow -join ', ')"
    }

    # Header count sub-check: KNOWN stale at this Work Package's own base
    # (states 142; 206 files actually present). The WP 16.2B backfill this
    # header's own count belongs to lands its content in docs/academy/**,
    # which is WP 16.2B's own file ownership, not this Work Package's - so
    # reported as Warn, not Fail, for this release only; promote to Fail
    # after WP 16.2B closure re-derives this header for real.
    $warnDetails = @()
    $actualFileCount = @($files).Count
    if ($headerMatch.Success)
    {
        $statedHeaderCount = [int]$headerMatch.Groups[1].Value
        if ($statedHeaderCount -ne $actualFileCount)
        {
            $warnDetails += "'## 03 Work Packages' header states ($statedHeaderCount retrospectives); docs/academy/03 Work Packages/ actually contains $actualFileCount file(s) - known stale at WP 16.1B's own base, promote to Fail after WP 16.2B closure."
        }
    }
    else
    {
        $warnDetails += "Could not locate the '## 03 Work Packages (N retrospectives)' header count in '$registerPath'."
    }

    $status = if ($failDetails.Count -gt 0) { "Fail" } elseif ($warnDetails.Count -gt 0) { "Warn" } else { "Pass" }
    New-CheckResult -Name "Academy Register covers every 03 Work Packages/ retrospective ($actualFileCount files checked)" -Status $status -Details ($failDetails + $warnDetails)
}

# ---------------------------------------------------------------------------
# Check 16 (WP 16.1B) - Documentation Register 03 Work Packages/ row count
# ---------------------------------------------------------------------------

function Test-DocumentationRegisterWorkPackagesCount
{
    param([Parameter(Mandatory)][string]$Root)

    $registerPath = Join-Path $Root "docs\governance\Documentation\Documentation Register.md"
    $wpDir = Join-Path $Root "docs\academy\03 Work Packages"

    if (!(Test-Path $registerPath) -or !(Test-Path $wpDir))
    {
        return New-CheckResult -Name "Documentation Register 03 Work Packages/ row matches file count" -Status Fail `
            -Details @("Expected paths not found: '$registerPath' and/or '$wpDir'.")
    }

    $actualFileCount = @(Get-ChildItem -LiteralPath $wpDir -Filter "*.md").Count

    $text = Get-Content -LiteralPath $registerPath -Raw
    $rowMatch = [regex]::Match($text, '\|\s*`docs/academy/03 Work Packages/`\s*\|\s*(\d+) Work Package retrospectives')

    if (-not $rowMatch.Success)
    {
        return New-CheckResult -Name "Documentation Register 03 Work Packages/ row matches file count" -Status Fail `
            -Details @("Could not locate the 'docs/academy/03 Work Packages/' row's stated retrospective count in '$registerPath'.")
    }

    $statedCount = [int]$rowMatch.Groups[1].Value

    # KNOWN stale at this Work Package's own base (states 165; 206 files
    # actually present) - the same WP 16.2B backfill gap Check 15's header
    # sub-check discloses, in a second document. Reported as Warn, not
    # Fail, for this release only; promote to Fail after WP 16.2B closure.
    $details = @()
    if ($statedCount -ne $actualFileCount)
    {
        $details += "'docs/academy/03 Work Packages/' row states $statedCount Work Package retrospectives; the directory actually contains $actualFileCount file(s) - known stale at WP 16.1B's own base, promote to Fail after WP 16.2B closure."
    }

    $status = if ($details.Count -eq 0) { "Pass" } else { "Warn" }
    New-CheckResult -Name "Documentation Register 03 Work Packages/ row matches file count ($actualFileCount files, $statedCount stated)" -Status $status -Details $details
}

# ---------------------------------------------------------------------------
# Run all checks, in a fixed, deterministic order
# ---------------------------------------------------------------------------

$checks = @(
    @{ Name = "ADR Register matches docs/adr/"; Action = { Test-AdrRegisterMatchesFiles -Root $RepoRoot } },
    @{ Name = "Academy Index matches docs/academy/ articles"; Action = { Test-AcademyIndexMatchesArticles -Root $RepoRoot } },
    @{ Name = "Release Register matches git tags"; Action = { Test-ReleaseRegisterMatchesTags -Root $RepoRoot } },
    @{ Name = "Documentation Register path references exist"; Action = { Test-DocumentationRegisterReferences -Root $RepoRoot } },
    @{ Name = "PROJECT_STATUS.md references are valid"; Action = { Test-ProjectStatusReferences -Root $RepoRoot } },
    @{ Name = "VERSION matches a planned release folder"; Action = { Test-VersionMatchesPlannedRelease -Root $RepoRoot } },
    @{ Name = "Release folders contain mandatory documentation"; Action = { Test-ReleaseFoldersHaveMandatoryDocs -Root $RepoRoot } },
    @{ Name = "Governance Index has no missing or orphaned documents"; Action = { Test-GovernanceIndexOrphans -Root $RepoRoot } },
    @{ Name = "Interface Register matches src/Tempest.Core/ public interfaces"; Action = { Test-InterfaceRegisterMatchesSource -Root $RepoRoot } },
    @{ Name = "Exception Register matches src/Tempest.Core/ exception classes"; Action = { Test-ExceptionRegisterMatchesSource -Root $RepoRoot } },
    @{ Name = "Namespace Register matches declared-scope source"; Action = { Test-NamespaceRegisterMatchesSource -Root $RepoRoot } },
    @{ Name = "Technical Debt Register summary matches its own rows"; Action = { Test-TechnicalDebtRegisterSummaryMatchesRows -Root $RepoRoot } },
    @{ Name = "Future Capability Register currency"; Action = { Test-FutureCapabilityRegisterCurrency -Root $RepoRoot } },
    @{ Name = "Governance Index ADR count matches docs/adr/"; Action = { Test-GovernanceIndexAdrCountMatchesFiles -Root $RepoRoot } },
    @{ Name = "Academy Register covers every 03 Work Packages/ retrospective"; Action = { Test-AcademyRegisterWorkPackagesCoverage -Root $RepoRoot } },
    @{ Name = "Documentation Register 03 Work Packages/ row matches file count"; Action = { Test-DocumentationRegisterWorkPackagesCount -Root $RepoRoot } }
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
        # TD-43: a check that throws is itself a Fail, not a silent skip -
        # but the report must name *which* check failed and *why*, not
        # swallow both into one hard-coded label (WP 11.2A's original
        # shape, empirically reproduced against a zero-byte register file
        # and a link-less index document, WP 11.9.0 QA Report). The
        # specific check's own declared Name, the exception's own runtime
        # type, and its message are all carried into the Fail result, so a
        # CI-gate failure is diagnosable from the report alone, without log
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
