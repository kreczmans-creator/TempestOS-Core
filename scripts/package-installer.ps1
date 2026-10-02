<#
.SYNOPSIS
    Publishes Tempest.Desktop self-contained for win-x64 and packages it
    into a Velopack installer (Setup.exe) plus its update-feed assets.

.DESCRIPTION
    WP 21.5A (`WP RC.0A` scope item 1). This is `release.yml`'s own
    "Package installer" step, factored out so it can be run and verified
    locally, on demand, with no dependency on `act` or any other
    GitHub-Actions-in-a-container tool - a developer (or this script's own
    tests) runs the exact same command CI runs.

    Two artefacts land in -OutputDirectory: `TempestOS-<Version>-Setup.exe`
    (renamed from Velopack's own `TempestOS-win-Setup.exe`, to match this
    project's own `TempestOS-<tag>...` asset-naming convention every other
    release.yml asset already follows) and, alongside it, every file
    Velopack's own update mechanism needs - `releases.win.json`,
    `assets.win.json`, the full `.nupkg` and the portable `.zip` - left
    under their own Velopack-generated names, unrenamed: `UpdateManager`
    locates them by that exact convention when it later checks a GitHub
    Release for a newer version, so renaming any of those (unlike the
    Setup.exe, which nothing reads by name) would break the very update
    check this Work Package exists to enable.

    Requires nothing pre-installed beyond the .NET SDK (`global.json`
    already pins the version CI uses): the `vpk` CLI is a local dotnet tool
    pinned in `.config/dotnet-tools.json` (1.2.0), restored with
    `dotnet tool restore` and run as `dotnet vpk`, so a clean machine (a
    fresh CI runner, a developer who has never run this before) needs no
    manual setup step, and a different vpk on PATH is never used.

.PARAMETER Version
    The Velopack package version (semver, e.g. "0.21.0"). Defaults to the
    repository's own root `VERSION` file.

.PARAMETER OutputDirectory
    Where the installer and update-feed assets are written. Defaults to
    `artifacts/installer` under the repository root. Cleared and recreated
    on every run, so a stale asset from an earlier version is never left
    beside a fresh one.

.EXAMPLE
    pwsh -NoProfile -File scripts/package-installer.ps1
    Packages the version named in the repository's own VERSION file.

.EXAMPLE
    pwsh -NoProfile -File scripts/package-installer.ps1 -Version 0.21.0
    Packages an explicit version - what release.yml itself does, passing
    the tag it is publishing.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$desktopProject = Join-Path $repoRoot "src/Tempest.Desktop/Tempest.Desktop.csproj"
$iconPath = Join-Path $repoRoot "src/Tempest.Desktop/Assets/Brand/tempestos.ico"
$publishDirectory = Join-Path $repoRoot "artifacts/installer-publish"

if (-not $Version) {
    $Version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot "artifacts/installer"
}

Write-Host "Packaging TempestOS installer, version $Version"

foreach ($directory in @($publishDirectory, $OutputDirectory)) {
    if (Test-Path $directory) {
        Remove-Item $directory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

Write-Host "Publishing Tempest.Desktop (Release, win-x64, self-contained)..."
dotnet publish $desktopProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:TreatWarningsAsErrors=true `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# The vpk CLI is pinned in the repository's own tool manifest
# (.config/dotnet-tools.json, vpk 1.2.0 - the same version as the Velopack
# PackageReference in Tempest.Desktop.csproj) and always run through
# `dotnet vpk`, never a bare `vpk` from PATH: a globally installed vpk of
# another version used to win silently (v0.23.0 CI board G-07, ADR-0160).
# `dotnet tool restore` resolves the manifest from the working directory,
# so both commands run from the repository root.
$expectedVpkVersion = "1.2.0"
Push-Location $repoRoot
try {
    Write-Host "Restoring pinned dotnet tools (vpk $expectedVpkVersion)..."
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet tool restore failed with exit code $LASTEXITCODE."
    }

    $manifest = Get-Content (Join-Path $repoRoot ".config/dotnet-tools.json") -Raw | ConvertFrom-Json
    $pinnedVpkVersion = $manifest.tools.vpk.version
    if ($pinnedVpkVersion -ne $expectedVpkVersion) {
        throw "The tool manifest pins vpk '$pinnedVpkVersion', but this script expects '$expectedVpkVersion'. Update both together (and the Velopack PackageReference)."
    }

    Write-Host "Running vpk pack..."
    dotnet vpk pack `
        --packId TempestOS `
        --packVersion $Version `
        --packDir $publishDirectory `
        --mainExe Tempest.Desktop.exe `
        --packTitle "TempestOS" `
        --packAuthors "Tempest Engineering" `
        --icon $iconPath `
        --outputDir $OutputDirectory
} finally {
    Pop-Location
}
if ($LASTEXITCODE -ne 0) {
    throw "vpk pack failed with exit code $LASTEXITCODE."
}

$setupSource = Join-Path $OutputDirectory "TempestOS-win-Setup.exe"
$setupDestination = Join-Path $OutputDirectory "TempestOS-$Version-Setup.exe"
if (Test-Path $setupSource) {
    Move-Item $setupSource $setupDestination -Force
} elseif (-not (Test-Path $setupDestination)) {
    throw "vpk pack did not produce the expected 'TempestOS-win-Setup.exe'."
}

Write-Host "Installer packaged at $OutputDirectory"
Get-ChildItem $OutputDirectory | Format-Table Name, Length
