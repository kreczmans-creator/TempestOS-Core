param(
    [Parameter(Mandatory)]
    [string]$Version,

    [switch]$Push
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

$releaseNotes = "docs/releases/v$Version.md"

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
# Build
#

Write-Host "Building..."

dotnet build src/TempestOS.slnx -c Release

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