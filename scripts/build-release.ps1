<#
.SYNOPSIS
    Build InboxDock release artifacts: portable ZIP, installer, and SHA256 checksums.

.DESCRIPTION
    Runs restore, build, test, publish, then packages portable ZIP and
    Inno Setup installer. Generates SHA256SUMS.txt for all artifacts.
    Requires Inno Setup (iscc) on PATH for installer generation.

.PARAMETER Version
    Version string (e.g. "0.3.0"). Defaults to value in Directory.Build.props.

.PARAMETER SkipTests
    Skip running tests before publish.

.PARAMETER SkipInstaller
    Skip building Inno Setup installer (if iscc not available).

.EXAMPLE
    .\scripts\build-release.ps1 -Version 0.3.0
#>
[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipTests,
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\.."
$artifactsDir = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsDir "publish"

if (-not $Version) {
    # Read version from Directory.Build.props
    $propsPath = Join-Path $repoRoot "Directory.Build.props"
    $props = [xml](Get-Content $propsPath)
    $Version = $props.Project.PropertyGroup.VersionPrefix
    if (-not $Version) {
        throw "Cannot determine version from Directory.Build.props"
    }
}

Write-Host "Building InboxDock v$Version" -ForegroundColor Cyan
Write-Host "Repo root: $repoRoot" -ForegroundColor DarkGray

# Clean artifacts
if (Test-Path $artifactsDir) {
    Remove-Item $artifactsDir -Recurse -Force
}
New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null

# Restore and build
Write-Host "`n=== Restore ===" -ForegroundColor Yellow
& dotnet restore (Join-Path $repoRoot "InboxDock.sln")
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

Write-Host "`n=== Build (Release) ===" -ForegroundColor Yellow
& dotnet build (Join-Path $repoRoot "InboxDock.sln") -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Tests
if (-not $SkipTests) {
    Write-Host "`n=== Test ===" -ForegroundColor Yellow
    & dotnet test (Join-Path $repoRoot "InboxDock.sln") -c Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed" }
}

# Publish self-contained
Write-Host "`n=== Publish ===" -ForegroundColor Yellow
& dotnet publish (Join-Path $repoRoot "src\InboxDock.App\InboxDock.App.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# Portable ZIP
$portableZip = Join-Path $artifactsDir "InboxDock-$Version-portable-win-x64.zip"
Write-Host "`n=== Portable ZIP ===" -ForegroundColor Yellow
Compress-Archive -Path "$publishDir\*" -DestinationPath $portableZip -CompressionLevel Optimal
Write-Host "Created: $portableZip" -ForegroundColor Green

# Inno Setup installer
if (-not $SkipInstaller) {
    $iscc = Get-Command "iscc" -ErrorAction SilentlyContinue
    if ($iscc) {
        Write-Host "`n=== Inno Setup Installer ===" -ForegroundColor Yellow
        $issPath = Join-Path $repoRoot "installer\InboxDock.iss"
        & iscc /DAppVersion=$Version $issPath
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Inno Setup build failed. Run with -SkipInstaller to skip."
        } else {
            Write-Host "Installer created in: $artifactsDir" -ForegroundColor Green
        }
    } else {
        Write-Warning "Inno Setup (iscc) not found on PATH. Skipping installer. Install Inno Setup 6 to build installer."
    }
} else {
    Write-Host "`n=== Installer skipped ===" -ForegroundColor DarkGray
}

# SHA256 checksums
Write-Host "`n=== SHA256 Checksums ===" -ForegroundColor Yellow
$checksumsPath = Join-Path $artifactsDir "SHA256SUMS.txt"
$checksums = @()
Get-ChildItem $artifactsDir -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()
    $checksums += "$hash  $($_.Name)"
    Write-Host "  $hash  $($_.Name)" -ForegroundColor DarkGray
}
$checksums | Set-Content -Path $checksumsPath -Encoding UTF8
Write-Host "Checksums: $checksumsPath" -ForegroundColor Green

# Summary
Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Get-ChildItem $artifactsDir -File | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name) ($sizeMB MB)" -ForegroundColor Green
}

Write-Host "`nDone. Artifacts in: $artifactsDir" -ForegroundColor Cyan
