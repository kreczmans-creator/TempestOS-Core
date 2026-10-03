<#
.SYNOPSIS
    Runs the v0.24.0 Xero live smoke test against the Xero Demo Company and
    prints what it found.

.DESCRIPTION
    ADR-0162, D7: TempestOS is tested against the Xero Demo Company before
    the live organisation is connected. This script runs the live smoke tests
    (tests/Tempest.Core.Tests, Category=XeroLive), which are skipped
    everywhere else, including CI.

    What the test does (about 35 calls, under Xero's 60 a minute):
      1. Connects with the tokens TempestOS stored when you connected Xero in
         Settings (or signs in first with -Connect).
      2. Checks the granted scopes, reads the organisation, tax rates,
         accounts and the bank summary.
      3. REFUSES TO WRITE unless Xero says the organisation IsDemoCompany.
         The Xero safety handler also runs with "Allow the live
         organisation" switched off, whatever Settings says.
      4. Links or creates the contact "TempestOS Smoke".
      5. Quote SMOKE-Q-<stamp>: DRAFT with PDF, a second PDF under the same
         name, then SENT, then ACCEPTED.
      6. Invoice SMOKE-INV-<stamp>: DRAFT with PDF; read back still DRAFT,
         never AUTHORISED, never sent to the contact.
      7. Purchase order SMOKE-PO-<stamp>: DRAFT with PDF.
      8. Expense bill SMOKE-EXP-<stamp>: DRAFT with a receipt.
      9. Repeats every create with the same Idempotency-Key and checks Xero
         holds one of each.
     10. Checks nothing that reached Xero approved, emailed or wrote outside
         the allow-list, then deletes the drafts (not with -Keep). The
         accepted quote is left; delete it in Xero if you want.
    With -KeyWindow it also runs the idempotency-key probe: about seven more
    minutes of waiting, to confirm Xero keeps a key longer than the 5
    minutes TempestOS assumes.

    The report (steps, open items, and a Xero link for every record) is
    written to -Report and printed at the end. No token is ever printed.

    Exit code: 0 when every step passed, 1 otherwise.

.PARAMETER DataFolder
    The TempestOS data folder whose secrets hold the Xero tokens - the one
    the app you connected Xero from uses. Defaults to
    C:\TempestOS-rc<minor>-data, as scripts/install-test-build.ps1 does,
    with <minor> read from the repository's VERSION file. Until VERSION is
    bumped to 0.24.0 at release that default is the rc23 folder, so for the
    v0.24.0 test build pass -DataFolder C:\TempestOS-rc24-data explicitly
    (the runbook and the setup guide always do).

.PARAMETER Connect
    Sign in first: a browser opens; choose the Demo Company. The new tokens
    are stored in -DataFolder, as Settings -> Xero -> Connect would.

.PARAMETER ClientId
    The Xero app's client id, when TempestOS has none stored (needed for
    -Connect, and to refresh an expired stored token). Ignored with
    -AccessToken: a supplied token is never refreshed, so no client id or
    secret is ever sent to Xero's token endpoint for it.

.PARAMETER ClientSecret
    The Xero app's client secret, when the app has one and TempestOS has
    none stored. Ignored with -AccessToken, as -ClientId is.

.PARAMETER AccessToken
    Use this access token instead of the stored ones (for example from a
    secret store). Needs -TenantId. It is never refreshed: it is used until
    2 minutes before the expiry in the token itself (Xero's tokens last 30
    minutes), or for 23 minutes when the token is opaque - supply a freshly
    issued one (and one with more than 10 minutes left for -KeyWindow).

.PARAMETER Scopes
    With an opaque -AccessToken only: the scopes it was granted, separated
    by spaces. Xero's tokens carry their own scope claim, which wins.
    Without either, the scope check is reported "not checked".

.PARAMETER TenantId
    The Demo Company's Xero tenant id, with -AccessToken.

.PARAMETER Keep
    Keep the smoke drafts in the Demo Company for inspection.

.PARAMETER KeyWindow
    Also run the idempotency-key retention probe (about seven minutes).

.PARAMETER Report
    Where the Markdown report is written. Defaults to
    artifacts/xero-smoke/xero-demo-smoke-<time>.md under the repository.

.EXAMPLE
    pwsh -NoProfile -File scripts/xero-demo-smoke.ps1

.EXAMPLE
    pwsh -NoProfile -File scripts/xero-demo-smoke.ps1 -Connect -ClientId <id> -Keep -KeyWindow
#>
[CmdletBinding()]
param(
    [string] $DataFolder,
    [switch] $Connect,
    [string] $ClientId,
    [string] $ClientSecret,
    [string] $AccessToken,
    [string] $TenantId,
    [string] $Scopes,
    [switch] $Keep,
    [switch] $KeyWindow,
    [string] $Report
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj"

if ($AccessToken -and -not $TenantId) {
    throw "-AccessToken needs -TenantId (the Demo Company's Xero tenant id)."
}

if (-not $DataFolder) {
    $version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
    $minor = ($version -split '\.')[1]
    $DataFolder = "C:\TempestOS-rc$minor-data"
}

if (-not $Report) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $Report = Join-Path $repoRoot "artifacts/xero-smoke/xero-demo-smoke-$stamp.md"
}
$reportFolder = Split-Path -Parent $Report
if ($reportFolder -and -not (Test-Path $reportFolder)) {
    New-Item -ItemType Directory -Path $reportFolder -Force | Out-Null
}

$variables = @{
    "TEMPEST_XERO_LIVE"          = "1"
    "TEMPEST_XERO_DATA_FOLDER"   = $DataFolder
    "TEMPEST_XERO_REPORT"        = $Report
    "TEMPEST_XERO_CONNECT"       = $(if ($Connect) { "1" } else { "" })
    "TEMPEST_XERO_KEEP"          = $(if ($Keep) { "1" } else { "" })
    "TEMPEST_XERO_KEY_WINDOW"    = $(if ($KeyWindow) { "1" } else { "" })
    "TEMPEST_XERO_CLIENT_ID"     = $ClientId
    "TEMPEST_XERO_CLIENT_SECRET" = $ClientSecret
    "TEMPEST_XERO_ACCESS_TOKEN"  = $AccessToken
    "TEMPEST_XERO_TENANT_ID"     = $TenantId
    "TEMPEST_XERO_SCOPES"        = $Scopes
}

Write-Host "=================================================================="
Write-Host " TempestOS v0.24.0 - Xero Demo Company smoke test"
Write-Host "=================================================================="
Write-Host " Data folder (stored tokens): $DataFolder"
if ($AccessToken) {
    Write-Host " Credentials:                 supplied access token (not refreshed)"
}
Write-Host " Sign in first:               $(if ($Connect) { 'yes - a browser window opens' } else { 'no' })"
Write-Host " Keep drafts:                 $(if ($Keep) { 'yes' } else { 'no - deleted at the end' })"
Write-Host " Key-retention probe:         $(if ($KeyWindow) { 'yes - about 7 more minutes' } else { 'no' })"
Write-Host " Report:                      $Report"
Write-Host ""
Write-Host " The test writes ONLY if Xero reports the organisation IsDemoCompany."
Write-Host "=================================================================="
Write-Host ""

$previous = @{}
foreach ($name in $variables.Keys) {
    $previous[$name] = [Environment]::GetEnvironmentVariable($name)
    $value = $variables[$name]
    if ($value) {
        [Environment]::SetEnvironmentVariable($name, $value)
    }
    else {
        [Environment]::SetEnvironmentVariable($name, $null)
    }
}

$exitCode = 1
try {
    dotnet test $project --filter "Category=XeroLive" --logger "console;verbosity=detailed"
    $exitCode = $LASTEXITCODE
}
finally {
    foreach ($name in $variables.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previous[$name])
    }
}

Write-Host ""
Write-Host "=================================================================="
$written = Get-ChildItem -Path (Split-Path -Parent $Report) -Filter ("{0}*{1}" -f [IO.Path]::GetFileNameWithoutExtension($Report), [IO.Path]::GetExtension($Report)) -ErrorAction SilentlyContinue
if ($written) {
    foreach ($file in $written) {
        Write-Host " Report: $($file.FullName)"
        Write-Host "------------------------------------------------------------------"
        Get-Content $file.FullName | ForEach-Object { Write-Host $_ }
        Write-Host "------------------------------------------------------------------"
    }
}
else {
    Write-Host " No report was written: the tests did not reach Xero (see the output above)."
}

if ($exitCode -eq 0) {
    Write-Host " RESULT: PASSED - open each record's link above and check it in Xero (runbook XL steps)."
}
else {
    Write-Host " RESULT: FAILED - see the failed steps above. Nothing is approved or emailed by this test."
}
Write-Host "=================================================================="

exit $exitCode
