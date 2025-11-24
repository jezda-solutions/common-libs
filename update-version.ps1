#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates the version number in all Jezda.Common NuGet packages.

.DESCRIPTION
    This script updates the <Version> tag in all Jezda.Common.*.csproj files (excluding test projects).
    It supports both incrementing the patch version automatically or setting a specific version.

.PARAMETER Version
    Specific version to set (e.g., "1.0.35"). If not provided, auto-increments the highest current version.

.PARAMETER Increment
    Which version component to increment: Major, Minor, or Patch (default: Patch).

.EXAMPLE
    # Auto-increment patch version (1.0.34 -> 1.0.35)
    ./update-version.ps1

.EXAMPLE
    # Set specific version
    ./update-version.ps1 -Version "2.0.0"

.EXAMPLE
    # Increment minor version (1.0.34 -> 1.1.0)
    ./update-version.ps1 -Increment Minor
#>

param(
    [string]$Version = $null,
    [ValidateSet("Major", "Minor", "Patch")]
    [string]$Increment = "Patch"
)

$ErrorActionPreference = "Stop"

# Find all Jezda.Common.*.csproj files (excluding Tests)
$projectFiles = Get-ChildItem -Path . -Filter "Jezda.Common.*.csproj" -Recurse |
    Where-Object { $_.FullName -notmatch "\\obj\\" -and $_.Name -notmatch "Tests" }

if ($projectFiles.Count -eq 0) {
    Write-Error "No Jezda.Common.*.csproj files found!"
    exit 1
}

Write-Host "Found $($projectFiles.Count) project files:" -ForegroundColor Cyan
$projectFiles | ForEach-Object { Write-Host "  - $($_.Name)" }
Write-Host ""

# Determine new version
if ([string]::IsNullOrEmpty($Version)) {
    # Extract current versions
    $currentVersions = @()
    foreach ($proj in $projectFiles) {
        $content = Get-Content $proj.FullName -Raw
        if ($content -match '<Version>([\d\.]+)</Version>') {
            $currentVersions += [System.Version]::Parse($Matches[1])
        }
    }

    if ($currentVersions.Count -eq 0) {
        Write-Error "No versions found in project files!"
        exit 1
    }

    # Find highest version
    $highestVersion = $currentVersions | Sort-Object -Descending | Select-Object -First 1
    Write-Host "Current highest version: $highestVersion" -ForegroundColor Yellow

    # Increment version
    switch ($Increment) {
        "Major" { $newVersion = New-Object System.Version($highestVersion.Major + 1, 0, 0) }
        "Minor" { $newVersion = New-Object System.Version($highestVersion.Major, $highestVersion.Minor + 1, 0) }
        "Patch" { $newVersion = New-Object System.Version($highestVersion.Major, $highestVersion.Minor, $highestVersion.Build + 1) }
    }
    $Version = $newVersion.ToString()
}

Write-Host "New version: $Version" -ForegroundColor Green
Write-Host ""

# Confirm with user
$response = Read-Host "Update all packages to version $Version? (y/N)"
if ($response -ne "y" -and $response -ne "Y") {
    Write-Host "Cancelled." -ForegroundColor Yellow
    exit 0
}

# Update all project files
$updatedCount = 0
foreach ($proj in $projectFiles) {
    $content = Get-Content $proj.FullName -Raw

    if ($content -match '<Version>[\d\.]+</Version>') {
        $newContent = $content -replace '<Version>[\d\.]+</Version>', "<Version>$Version</Version>"
        Set-Content -Path $proj.FullName -Value $newContent -NoNewline
        Write-Host "[UPDATED] $($proj.Name)" -ForegroundColor Green
        $updatedCount++
    }
    else {
        Write-Host "[SKIPPED] $($proj.Name) - No <Version> tag found" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Updated $updatedCount project(s) to version $Version" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Build all projects: dotnet build -c Release" -ForegroundColor Gray
Write-Host "  2. Review changes: git diff" -ForegroundColor Gray
Write-Host "  3. Commit changes: git add . && git commit -m 'Bump version to $Version'" -ForegroundColor Gray
Write-Host "  4. Create tag: git tag v$Version" -ForegroundColor Gray
Write-Host "  5. Push: git push && git push --tags" -ForegroundColor Gray
