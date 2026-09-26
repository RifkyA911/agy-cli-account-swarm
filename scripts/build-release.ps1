<#
.SYNOPSIS
    Automated build & packaging script for agy-cli-account-swarm Release.
#>
param (
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"

Write-Host "Restoring solution dependencies..." -ForegroundColor Cyan
dotnet restore

Write-Host "Compiling Release binary for net9.0-windows..." -ForegroundColor Cyan
dotnet publish -c Release -o $OutputDir

# Copy application icon to Resources
$resDir = Join-Path $OutputDir "Resources"
if (-not (Test-Path $resDir)) {
    New-Item -ItemType Directory -Path $resDir -Force | Out-Null
}

$iconSrc = "C:\Users\rifky\Downloads\favicon.ico"
if (Test-Path $iconSrc) {
    Copy-Item $iconSrc (Join-Path $resDir "favicon.ico") -Force
    Write-Host "Icon copied to $resDir\favicon.ico" -ForegroundColor Green
}

Write-Host "Release compilation finished at $(Get-Date). Binary located in: $OutputDir" -ForegroundColor Green
