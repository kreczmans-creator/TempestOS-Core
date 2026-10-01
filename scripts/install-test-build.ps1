<#
.SYNOPSIS
    Builds the installer for the checked-out branch, installs it, and puts a
    versioned "TempestOS <version> (test)" shortcut on the desktop.

.DESCRIPTION
    PO decision 2026-10-01: every build handed over for testing comes with
    its own installer and shortcut, so an old desktop shortcut can never be
    mistaken for the build under test (the v0.23.0 runbook was started on a
    v0.22.0 shortcut, which opened a different data folder and looked like
    lost projects).

    Steps, each one stopping the script on failure:
      1. Optionally pulls the current branch (-Pull).
      2. Packages the installer with scripts/package-installer.ps1 (the same
         command release.yml runs), at the version in the root VERSION file.
      3. Closes any running TempestOS, then runs the Setup.exe it just built.
         Velopack installs per user to %LOCALAPPDATA%\TempestOS and replaces
         whatever version was installed before, so the existing "TempestOS"
         Start menu and desktop shortcuts now open this build too.
      4. If no first-run choice is recorded yet, records the test data folder
         as the choice, so the first launch skips the data-location dialog
         (C-01: that dialog can hang). An existing choice is left alone.
      5. Replaces any older "TempestOS * (test)" desktop shortcut with one
         named for this version, which opens the installed app on the test
         data folder via --persistence-root.
      6. Checks the installed exe's version matches VERSION and prints it,
         with the commit, so the runbook's "Title-bar build" field can be
         filled in.

.PARAMETER DataFolder
    The data folder the versioned shortcut opens. Defaults to
    C:\TempestOS-rc<minor>-data (C:\TempestOS-rc23-data for 0.23.x), so a
    retest of the same version keeps its projects and a new version starts
    clean.

.PARAMETER Pull
    Run `git pull` on the current branch first.

.EXAMPLE
    pwsh -NoProfile -File scripts/install-test-build.ps1 -Pull
#>
[CmdletBinding()]
param(
    [string] $DataFolder,
    [switch] $Pull
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if ($Pull) {
    Write-Host "Pulling the current branch..."
    git -C $repoRoot pull
    if ($LASTEXITCODE -ne 0) {
        throw "git pull failed with exit code $LASTEXITCODE."
    }
}

$version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
$commit = (git -C $repoRoot rev-parse --short HEAD).Trim()
$branch = (git -C $repoRoot rev-parse --abbrev-ref HEAD).Trim()

if (-not $DataFolder) {
    $minor = ($version -split '\.')[1]
    $DataFolder = "C:\TempestOS-rc$minor-data"
}

Write-Host "Building the TempestOS $version test installer from $branch @ $commit"
& (Join-Path $PSScriptRoot "package-installer.ps1") -Version $version
$setup = Join-Path $repoRoot "artifacts/installer/TempestOS-$version-Setup.exe"
if (-not (Test-Path $setup)) {
    throw "The installer was not produced at $setup."
}

Write-Host "Closing any running TempestOS..."
Get-Process Tempest.Desktop, Tempest.Harness -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

$installedData = Join-Path $env:LOCALAPPDATA "TempestOS"
$marker = Join-Path $installedData "first-run.json"
if (-not (Test-Path $marker)) {
    New-Item -ItemType Directory -Path $installedData -Force | Out-Null
    @{ PersistenceRoot = $DataFolder } | ConvertTo-Json | Set-Content -Path $marker -Encoding UTF8
    Write-Host "Recorded $DataFolder as the installed app's data folder."
} else {
    Write-Host "Left the existing data-folder choice in $marker unchanged."
}

Write-Host "Installing $setup ..."
$process = Start-Process -FilePath $setup -ArgumentList "--silent" -PassThru -Wait
if ($process.ExitCode -ne 0) {
    throw "Setup exited with code $($process.ExitCode)."
}
Start-Sleep -Seconds 3
Get-Process Tempest.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force

$exe = Join-Path $installedData "current\Tempest.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "The installed app was not found at $exe."
}
$installedVersion = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $installedVersion.StartsWith($version)) {
    throw "Installed version is '$installedVersion', expected $version."
}

$desktop = [Environment]::GetFolderPath("Desktop")
Get-ChildItem $desktop -Filter "TempestOS * (test).lnk" -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "Removed the old shortcut $($_.Name)."
}
New-Item -ItemType Directory -Path $DataFolder -Force | Out-Null
$shortcutPath = Join-Path $desktop "TempestOS $version (test).lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.Arguments = "--persistence-root `"$DataFolder`""
$shortcut.WorkingDirectory = Split-Path -Parent $exe
$shortcut.Description = "TempestOS $version test build ($branch @ $commit), data in $DataFolder"
$shortcut.IconLocation = "$exe,0"
$shortcut.Save()

Write-Host ""
Write-Host "Installed TempestOS $installedVersion ($branch @ $commit)."
Write-Host "Desktop shortcut: TempestOS $version (test) -> data folder $DataFolder"
Write-Host "Runbook 'Title-bar build' field: TempestOS $version ($commit)"
