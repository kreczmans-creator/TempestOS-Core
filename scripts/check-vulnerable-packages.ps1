<#
.SYNOPSIS
    Parses `dotnet list <solution> package --vulnerable --include-transitive`
    output and fails if any project reports a vulnerable package.

.DESCRIPTION
    WP 21.5E / WP RC.0C ("Dependency vulnerability scan is a required CI
    check"). `dotnet list package --vulnerable` itself always exits 0 -
    whether or not it finds anything - so CI needs something that actually
    reads its output and turns a real finding into a failed step. This
    script is that something, split out of ci.yml so it can be exercised by
    -SelfTest without a network call or a live NuGet feed.

    The parse rule (stated in WP 21.5E's own brief): a
    "<project> has no vulnerable packages" line per project is the pass;
    anything else - a "<project> has the following vulnerable packages"
    line, or no per-project line at all (a parser mismatch, a crashed scan,
    an empty run) - is a fail. Silence is never treated as success: a run
    that produced zero recognisable project lines fails loudly rather than
    reporting "0 vulnerabilities found" for a scan that may never have run.

.PARAMETER InputPath
    Path to a text file holding the captured output of
    `dotnet list <solution> package --vulnerable --include-transitive`.
    Required unless -SelfTest is supplied.

.PARAMETER SelfTest
    Runs this script's own parser against two synthetic fixtures - one
    clean, one carrying a synthetic vulnerable package - and asserts each is
    classified correctly. Exits non-zero if either assertion fails. No
    -InputPath is read in this mode.

.EXAMPLE
    dotnet list src/TempestOS.slnx package --vulnerable --include-transitive `
        | Tee-Object -FilePath dependency-scan.log
    powershell -NoProfile -File scripts/check-vulnerable-packages.ps1 -InputPath dependency-scan.log

.EXAMPLE
    powershell -NoProfile -File scripts/check-vulnerable-packages.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$InputPath,
    [switch]$SelfTest
)

# The one piece of parsing logic both the real CI step and -SelfTest run -
# kept as a single function so a self-test that passes is a genuine promise
# about the code path CI actually exercises, not a parallel reimplementation
# that could drift from it.
function Test-VulnerablePackagesOutput {
    param([string[]]$Lines)

    # Matches both phrasings `dotnet list package` has used across SDK
    # versions - "Project `X` has ..." and "The given project `X` has ..." -
    # without anchoring on which one the SDK in CI happens to print.
    $projectLines = $Lines | Where-Object {
        $_ -match 'project `[^`]+` has (no vulnerable packages|the following vulnerable packages)'
    }

    if ($projectLines.Count -eq 0) {
        return [pscustomobject]@{
            Passed = $false
            Reason = "No '<project> has ...' line was found anywhere in the scan output - " +
                     "the parser could not confirm any project was actually scanned. " +
                     "Treating an unparsable or empty scan as a failure, not a silent pass."
        }
    }

    $vulnerable = $projectLines | Where-Object { $_ -match 'has the following vulnerable packages' }

    if ($vulnerable.Count -gt 0) {
        return [pscustomobject]@{
            Passed = $false
            Reason = "Vulnerable package(s) reported by $($vulnerable.Count) of $($projectLines.Count) scanned project(s):`n" +
                     (($vulnerable | ForEach-Object { "  $_" }) -join "`n")
        }
    }

    return [pscustomobject]@{
        Passed = $true
        Reason = "$($projectLines.Count) project(s) scanned; every one reports no vulnerable packages."
    }
}

if ($SelfTest) {
    $failures = 0

    # Fixture 1: a clean run, shaped exactly like this solution's own
    # 2026-09-15 baseline (WP 21.5E brief) - eight projects, none vulnerable.
    $clean = @(
        'The following sources were used:',
        '   https://api.nuget.org/v3/index.json',
        '',
        'The given project `Tempest.Core` has no vulnerable packages given the current sources.',
        'The given project `Tempest.Desktop` has no vulnerable packages given the current sources.'
    )
    $cleanResult = Test-VulnerablePackagesOutput -Lines $clean
    if ($cleanResult.Passed) {
        Write-Host "PASS: clean output is correctly classified as passing."
    }
    else {
        Write-Error "FAIL: clean output was misclassified as failing. $($cleanResult.Reason)"
        $failures++
    }

    # Fixture 2: a synthetic vulnerable line - the exact shape
    # `dotnet list package --vulnerable` prints for a real finding.
    $vulnerable = @(
        'The following sources were used:',
        '   https://api.nuget.org/v3/index.json',
        '',
        'The given project `Tempest.Core` has no vulnerable packages given the current sources.',
        'Project `Tempest.Desktop` has the following vulnerable packages',
        '   [net10.0]:',
        '   Top-level Package   Requested   Resolved   Severity   Advisory URL',
        '   > Newtonsoft.Json   12.0.1      12.0.1     High       https://github.com/advisories/GHSA-synthetic-test-0000'
    )
    $vulnerableResult = Test-VulnerablePackagesOutput -Lines $vulnerable
    if (-not $vulnerableResult.Passed) {
        Write-Host "PASS: a synthetic vulnerable line is correctly classified as failing."
    }
    else {
        Write-Error "FAIL: a synthetic vulnerable line was misclassified as passing."
        $failures++
    }

    # Fixture 3: no recognisable project line at all (an empty or wholly
    # unparsable scan) must fail, not silently pass as "0 vulnerabilities".
    $empty = @('', 'some unrelated restore noise', '')
    $emptyResult = Test-VulnerablePackagesOutput -Lines $empty
    if (-not $emptyResult.Passed) {
        Write-Host "PASS: an unparsable/empty scan is correctly classified as failing."
    }
    else {
        Write-Error "FAIL: an unparsable/empty scan was misclassified as passing."
        $failures++
    }

    if ($failures -gt 0) {
        Write-Error "$failures self-test assertion(s) failed."
        exit 1
    }

    Write-Host "All self-test assertions passed."
    exit 0
}

if (-not $InputPath) {
    Write-Error "Either -SelfTest or -InputPath must be supplied."
    exit 1
}

if (-not (Test-Path -LiteralPath $InputPath)) {
    Write-Error "Input file '$InputPath' does not exist."
    exit 1
}

$lines = Get-Content -LiteralPath $InputPath
$result = Test-VulnerablePackagesOutput -Lines $lines

Write-Host $result.Reason

if (-not $result.Passed) {
    exit 1
}

exit 0
