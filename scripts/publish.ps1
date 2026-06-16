# Ninja Security — Build & Publish Script
# Usage: .\scripts\publish.ps1 [-Configuration Release] [-Version 1.0.0]
# Run from the repo root.

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$Runtime = "win-x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot  = $PSScriptRoot | Split-Path
$PublishDir = Join-Path $RepoRoot "publish"
$ServiceOut = Join-Path $PublishDir "service"
$AppOut     = Join-Path $PublishDir "app"

Write-Host "=== Ninja Security Build ===" -ForegroundColor Cyan
Write-Host "Version: $Version  |  Runtime: $Runtime  |  Config: $Configuration"

# Clean
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item $ServiceOut -ItemType Directory | Out-Null
New-Item $AppOut     -ItemType Directory | Out-Null

# Publish service
Write-Host "`nPublishing service..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\src\NinjaSecurity.Service\NinjaSecurity.Service.csproj" `
    -c $Configuration -r $Runtime --self-contained true `
    -p:Version=$Version -p:AssemblyVersion=$Version `
    -o $ServiceOut
if ($LASTEXITCODE -ne 0) { throw "Service publish failed" }

# Publish app
Write-Host "`nPublishing GUI..." -ForegroundColor Yellow
dotnet publish "$RepoRoot\src\NinjaSecurity.App\NinjaSecurity.App.csproj" `
    -c $Configuration -r $Runtime --self-contained true `
    -p:Version=$Version -p:AssemblyVersion=$Version `
    -o $AppOut
if ($LASTEXITCODE -ne 0) { throw "App publish failed" }

# SHA-256 hashes
Write-Host "`nGenerating SHA-256 hashes..." -ForegroundColor Yellow
$HashFile = Join-Path $PublishDir "SHA256SUMS.txt"
@(
    Join-Path $ServiceOut "NinjaSecurity.Service.exe",
    Join-Path $AppOut     "NinjaSecurity.App.exe"
) | Where-Object { Test-Path $_ } | ForEach-Object {
    $hash = (Get-FileHash $_ -Algorithm SHA256).Hash
    $name = Split-Path $_ -Leaf
    "$hash  $name" | Tee-Object -FilePath $HashFile -Append
}

# Build installer (requires makensis in PATH)
if (Get-Command makensis -ErrorAction SilentlyContinue) {
    Write-Host "`nBuilding NSIS installer..." -ForegroundColor Yellow
    Push-Location $RepoRoot
    makensis installer/ninja-security.nsi
    Pop-Location
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer: NinjaSecuritySetup-$Version.exe" -ForegroundColor Green
    }
} else {
    Write-Host "`nSkipping installer (makensis not found in PATH)" -ForegroundColor DarkYellow
    Write-Host "Install NSIS from https://nsis.sourceforge.io/ then re-run."
}

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "Service : $ServiceOut"
Write-Host "App     : $AppOut"
Write-Host "Hashes  : $HashFile"
