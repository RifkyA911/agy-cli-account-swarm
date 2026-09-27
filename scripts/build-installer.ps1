# ==============================================================================
# Automated Release & Packaging Script for Agy CLI Account Swarm
# ==============================================================================
param (
    [string]$Version = "0.9.3-beta"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$PublishDir = Join-Path $ProjectRoot "publish"
$DistDir = Join-Path $ProjectRoot "dist"
$IssFile = Join-Path $ScriptDir "installer.iss"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Building Agy CLI Account Swarm Release Packages (v$Version)" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Ensure dist directory exists
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

# 2. Publish .NET 9 WPF Release
Write-Host ""
Write-Host "[1/4] Publishing Release binaries to $PublishDir..." -ForegroundColor Yellow
Push-Location $ProjectRoot
try {
    dotnet publish AgyAccountSwarm.csproj -c Release -o $PublishDir --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
Write-Host "[OK] Binaries successfully compiled." -ForegroundColor Green

# 3. Locate Inno Setup Compiler (ISCC.exe)
Write-Host ""
Write-Host "[2/4] Locating Inno Setup 6 compiler..." -ForegroundColor Yellow
$LocalISCC = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
$ProgramFilesISCC = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$ProgramFiles64ISCC = "C:\Program Files\Inno Setup 6\ISCC.exe"

$IsccPath = ""
if (Test-Path $LocalISCC) {
    $IsccPath = $LocalISCC
} elseif (Test-Path $ProgramFilesISCC) {
    $IsccPath = $ProgramFilesISCC
} elseif (Test-Path $ProgramFiles64ISCC) {
    $IsccPath = $ProgramFiles64ISCC
} else {
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        $IsccPath = $cmd.Source
    }
}

if ($IsccPath -ne "") {
    Write-Host "Found Inno Setup compiler: $IsccPath" -ForegroundColor DarkGray
    Write-Host "Compiling Windows Installer (.exe) with embedded uninstaller..." -ForegroundColor Yellow
    & "$IsccPath" "/Qp" "$IssFile"
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC compilation failed with exit code $LASTEXITCODE"
    }
    Write-Host "[OK] Windows Installer generated in $DistDir" -ForegroundColor Green
} else {
    Write-Warning "ISCC.exe not found. Skipping .exe installer generation."
    Write-Warning "Install via: winget install JRSoftware.InnoSetup"
}

# 4. Package Portable ZIP
Write-Host ""
Write-Host "[3/4] Packaging portable Windows x64 ZIP..." -ForegroundColor Yellow
$ZipPath = Join-Path $DistDir "Agy-CLI-Account-Swarm-v$Version-win-x64.zip"
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "[OK] Portable ZIP generated: $ZipPath" -ForegroundColor Green

# 5. Generate Checksums
Write-Host ""
Write-Host "[4/4] Computing SHA256 checksums..." -ForegroundColor Yellow
$ChecksumFile = Join-Path $DistDir "SHA256SUMS.txt"
$distFiles = Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" }

$lines = [System.Collections.Generic.List[string]]::new()
foreach ($file in $distFiles) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $lines.Add("$hash  $($file.Name)")
}
[System.IO.File]::WriteAllLines($ChecksumFile, $lines)
Write-Host "[OK] Checksums saved to: $ChecksumFile" -ForegroundColor Green

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Distribution artifacts built successfully:" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Get-ChildItem -Path $DistDir | Format-Table Name, Length, LastWriteTime
