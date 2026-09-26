<#
.SYNOPSIS
    Initializes a sandboxed isolated profile directory for Google Antigravity CLI.
.DESCRIPTION
    Creates the required directory tree including ~/.gemini and ~/.config, verifying
    path strings to avoid trailing whitespace errors.
.PARAMETER ProfileName
    Name or identifier for the profile sandbox.
#>
param (
    [Parameter(Mandatory=$true)]
    [string]$ProfileName
)

$baseDir = Join-Path $env:USERPROFILE ".gemini-profiles"
$profileDir = Join-Path $baseDir $ProfileName.Trim()

if (-not (Test-Path $profileDir)) {
    Write-Host "Creating sandbox directory: $profileDir" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $profileDir ".gemini") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $profileDir ".config") -Force | Out-Null
    Write-Host "Sandbox initialized successfully for '$ProfileName'." -ForegroundColor Green
} else {
    Write-Host "Profile sandbox '$profileDir' already exists." -ForegroundColor Yellow
}
