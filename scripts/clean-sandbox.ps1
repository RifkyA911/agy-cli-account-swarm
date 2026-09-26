<#
.SYNOPSIS
    Cleans up stale crash logs, lock files, and temp artifacts across profile sandboxes.
#>
param (
    [switch]$Force
)

$baseDir = Join-Path $env:USERPROFILE ".gemini-profiles"
if (-not (Test-Path $baseDir)) {
    Write-Host "No sandbox profiles directory found at $baseDir." -ForegroundColor Yellow
    exit 0
}

Write-Host "Scanning sandboxes for stale locks and crash logs..." -ForegroundColor Cyan
$crashLogs = Get-ChildItem -Path $baseDir -Recurse -Filter "*.log" -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*crash*" }

foreach ($log in $crashLogs) {
    Write-Host "Removing crash log: $($log.FullName)" -ForegroundColor Red
    Remove-Item $log.FullName -Force
}

Write-Host "Cleanup completed." -ForegroundColor Green
