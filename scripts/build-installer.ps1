# ==============================================================================
# Master Automated Release & Packaging Script for Agy CLI Account Swarm
# Builds Complete Distribution Packages for:
#   - Windows x64 (Setup .exe + Portable .zip)
#   - Windows ARM64 (Portable .zip)
#   - Linux x64 (tar.gz + Installer script + .desktop)
#   - Linux ARM64 (tar.gz + Installer script + .desktop)
#   - macOS x64 Intel (tar.gz + .app Bundle + Installer script)
#   - macOS ARM64 Apple Silicon (tar.gz + .app Bundle + Installer script)
# ==============================================================================
param (
    [string]$Version = "0.9.13-beta",
    [switch]$WindowsOnly = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$PublishDir = Join-Path $ProjectRoot "publish"
$DistDir = Join-Path $ProjectRoot "dist"
$StagingDir = Join-Path $DistDir "staging"
$IssFile = Join-Path $ScriptDir "installer.iss"
$Csproj = Join-Path $ProjectRoot "AgyAccountSwarm.Avalonia/AgyAccountSwarm.Avalonia.csproj"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "  Building Agy CLI Account Swarm Release Matrix (v$Version)" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

# 1. Clean & prepare directories
if (Test-Path $StagingDir) {
    Remove-Item -Path $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
}
if (Test-Path $PublishDir) {
    Remove-Item -Path "$PublishDir\*" -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
}
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}
# Clean existing archives in dist to avoid stale artifacts
Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -like "*$Version*" -or $_.Name -eq "SHA256SUMS.txt" } | Remove-Item -Force

New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

# Helper function to create tar.gz safely on Windows
function Create-TarGz {
    param(
        [string]$ArchivePath,
        [string]$WorkDir,
        [string]$ItemName
    )
    if (Test-Path $ArchivePath) { Remove-Item $ArchivePath -Force }
    $sysTar = "C:\Windows\System32\tar.exe"
    if (Test-Path $sysTar) {
        & "$sysTar" -czf "$ArchivePath" -C "$WorkDir" "$ItemName"
    } else {
        & tar --force-local -czf "$ArchivePath" -C "$WorkDir" "$ItemName"
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to create archive: $ArchivePath (exit code $LASTEXITCODE)"
    }
}

# Helper to create Info.plist for macOS bundles
function Write-MacPlist {
    param([string]$FilePath, [string]$Ver)
    $content = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>agy-cli-account-swarm</string>
    <key>CFBundleIdentifier</key>
    <string>com.rifkya911.agy-cli-account-swarm</string>
    <key>CFBundleName</key>
    <string>Agy CLI Account Swarm</string>
    <key>CFBundleVersion</key>
    <string>$Ver</string>
    <key>CFBundleShortVersionString</key>
    <string>$Ver</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>favicon.ico</string>
</dict>
</plist>
"@
    [System.IO.File]::WriteAllText($FilePath, $content, [System.Text.Encoding]::UTF8)
}

# Helper to create launcher script for macOS bundles
function Write-MacLauncher {
    param([string]$FilePath)
    $content = @'
#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RES_DIR="$(cd "$SCRIPT_DIR/../Resources" && pwd)"

# Native .NET 9 Avalonia execution on macOS (Metal / Cocoa)
if [ -x "$RES_DIR/AgyCliAccountSwarmGUI" ]; then
    exec "$RES_DIR/AgyCliAccountSwarmGUI" "$@"
elif [ -f "$RES_DIR/AgyCliAccountSwarmGUI.dll" ]; then
    exec dotnet "$RES_DIR/AgyCliAccountSwarmGUI.dll" "$@"
elif command -v wine >/dev/null 2>&1 && [ -f "$RES_DIR/AgyCliAccountSwarmGUI.exe" ]; then
    exec wine "$RES_DIR/AgyCliAccountSwarmGUI.exe" "$@"
else
    echo "Error: Could not locate AgyCliAccountSwarmGUI in $RES_DIR"
    exit 1
fi
'@
    # Write using Unix line endings (LF)
    $unixContent = $content.Replace("`r`n", "`n")
    [System.IO.File]::WriteAllText($FilePath, $unixContent, [System.Text.Encoding]::UTF8)
}

# ------------------------------------------------------------------------------
# [1/6] Windows x64 (Installer .exe + Portable .zip)
# ------------------------------------------------------------------------------
Write-Host ""
Write-Host "[1/6] Compiling Windows x64 binaries & packaging..." -ForegroundColor Yellow
dotnet publish $Csproj -c Release -r win-x64 --self-contained false -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Windows x64 publish failed" }

# Inno Setup compiler detection
$LocalISCC = Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"
$ProgramFilesISCC = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$ProgramFiles64ISCC = "C:\Program Files\Inno Setup 6\ISCC.exe"
$IsccPath = ""
if (Test-Path $LocalISCC) { $IsccPath = $LocalISCC }
elseif (Test-Path $ProgramFilesISCC) { $IsccPath = $ProgramFilesISCC }
elseif (Test-Path $ProgramFiles64ISCC) { $IsccPath = $ProgramFiles64ISCC }
else {
    $cmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($cmd) { $IsccPath = $cmd.Source }
}

if ($IsccPath -ne "") {
    Write-Host "  -> Compiling Inno Setup installer..." -ForegroundColor DarkGray
    & "$IsccPath" "/Qp" "$IssFile"
    if ($LASTEXITCODE -ne 0) { throw "ISCC compilation failed" }
    Write-Host "  [OK] Generated: Agy-CLI-Account-Swarm-Setup-v$Version.exe" -ForegroundColor Green
} else {
    Write-Warning "  ISCC.exe not found. Skipped Inno Setup .exe installer."
}

# Sign published binaries and installer if certificate is available
$signScript = Join-Path $ScriptDir "sign-release.ps1"
if (Test-Path $signScript) {
    try {
        & "$signScript"
    } catch {
        Write-Warning "Signing step encountered an error: $_"
    }
}

$WinX64Zip = Join-Path $DistDir "Agy-CLI-Account-Swarm-v$Version-win-x64.zip"
if (Test-Path $WinX64Zip) { Remove-Item $WinX64Zip -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $WinX64Zip -CompressionLevel Optimal
Write-Host "  [OK] Generated: Agy-CLI-Account-Swarm-v$Version-win-x64.zip" -ForegroundColor Green

# ------------------------------------------------------------------------------
# [2/6] Windows ARM64 (Portable .zip)
# ------------------------------------------------------------------------------
Write-Host ""
Write-Host "[2/6] Compiling Windows ARM64 binaries & packaging..." -ForegroundColor Yellow
$WinArm64Dir = Join-Path $StagingDir "win-arm64"
dotnet publish $Csproj -c Release -r win-arm64 --self-contained false -o $WinArm64Dir --nologo
if ($LASTEXITCODE -ne 0) { throw "Windows ARM64 publish failed" }

$WinArm64Zip = Join-Path $DistDir "Agy-CLI-Account-Swarm-v$Version-win-arm64.zip"
if (Test-Path $WinArm64Zip) { Remove-Item $WinArm64Zip -Force }
Compress-Archive -Path "$WinArm64Dir\*" -DestinationPath $WinArm64Zip -CompressionLevel Optimal
Write-Host "  [OK] Generated: Agy-CLI-Account-Swarm-v$Version-win-arm64.zip" -ForegroundColor Green

if (-not $WindowsOnly) {
    # --------------------------------------------------------------------------
    # [3/6] Linux x64 (.tar.gz with installer)
    # --------------------------------------------------------------------------
    Write-Host ""
    Write-Host "[3/6] Compiling Linux x64 binaries & packaging..." -ForegroundColor Yellow
    $LinuxX64Payload = Join-Path $StagingDir "linux-x64/agy-cli-account-swarm-v$Version-linux-x64"
    New-Item -ItemType Directory -Path $LinuxX64Payload -Force | Out-Null
    dotnet publish $Csproj -c Release -r linux-x64 --self-contained false -o $LinuxX64Payload --nologo
    if ($LASTEXITCODE -ne 0) { throw "Linux x64 publish failed" }

    # Copy installer scripts and helpers
    $linuxScriptsDir = Join-Path $LinuxX64Payload "scripts"
    New-Item -ItemType Directory -Path $linuxScriptsDir -Force | Out-Null
    Copy-Item (Join-Path $ScriptDir "install-linux.sh") (Join-Path $LinuxX64Payload "install.sh") -Force
    Copy-Item (Join-Path $ScriptDir "install-linux.sh") (Join-Path $linuxScriptsDir "install-linux.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-linux.sh") (Join-Path $LinuxX64Payload "uninstall.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-linux.sh") (Join-Path $linuxScriptsDir "uninstall-linux.sh") -Force
    if (Test-Path (Join-Path $ProjectRoot "docs\assets\banner.svg")) {
        Copy-Item (Join-Path $ProjectRoot "docs\assets\banner.svg") (Join-Path $LinuxX64Payload "banner.svg") -Force
    }

    $LinuxX64TarGz = Join-Path $DistDir "Agy-CLI-Account-Swarm-v$Version-linux-x64.tar.gz"
    Create-TarGz -ArchivePath $LinuxX64TarGz -WorkDir (Join-Path $StagingDir "linux-x64") -ItemName "agy-cli-account-swarm-v$Version-linux-x64"
    Write-Host "  [OK] Generated: Agy-CLI-Account-Swarm-v$Version-linux-x64.tar.gz" -ForegroundColor Green

    # --------------------------------------------------------------------------
    # [4/6] Linux ARM64 (.tar.gz with installer)
    # --------------------------------------------------------------------------
    Write-Host ""
    Write-Host "[4/6] Compiling Linux ARM64 binaries & packaging..." -ForegroundColor Yellow
    $LinuxArm64Payload = Join-Path $StagingDir "linux-arm64/agy-cli-account-swarm-v$Version-linux-arm64"
    New-Item -ItemType Directory -Path $LinuxArm64Payload -Force | Out-Null
    dotnet publish $Csproj -c Release -r linux-arm64 --self-contained false -o $LinuxArm64Payload --nologo
    if ($LASTEXITCODE -ne 0) { throw "Linux ARM64 publish failed" }

    # Copy installer scripts and helpers
    $linuxArmScriptsDir = Join-Path $LinuxArm64Payload "scripts"
    New-Item -ItemType Directory -Path $linuxArmScriptsDir -Force | Out-Null
    Copy-Item (Join-Path $ScriptDir "install-linux.sh") (Join-Path $LinuxArm64Payload "install.sh") -Force
    Copy-Item (Join-Path $ScriptDir "install-linux.sh") (Join-Path $linuxArmScriptsDir "install-linux.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-linux.sh") (Join-Path $LinuxArm64Payload "uninstall.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-linux.sh") (Join-Path $linuxArmScriptsDir "uninstall-linux.sh") -Force
    if (Test-Path (Join-Path $ProjectRoot "docs\assets\banner.svg")) {
        Copy-Item (Join-Path $ProjectRoot "docs\assets\banner.svg") (Join-Path $LinuxArm64Payload "banner.svg") -Force
    }

    $LinuxArm64TarGz = Join-Path $DistDir "Agy-CLI-Account-Swarm-v$Version-linux-arm64.tar.gz"
    Create-TarGz -ArchivePath $LinuxArm64TarGz -WorkDir (Join-Path $StagingDir "linux-arm64") -ItemName "agy-cli-account-swarm-v$Version-linux-arm64"
    Write-Host "  [OK] Generated: Agy-CLI-Account-Swarm-v$Version-linux-arm64.tar.gz" -ForegroundColor Green

    # --------------------------------------------------------------------------
    # [5/6] macOS x64 Intel (.app Bundle + .tar.gz)
    # --------------------------------------------------------------------------
    Write-Host ""
    Write-Host "[5/6] Compiling macOS x64 binaries & packaging .app bundle..." -ForegroundColor Yellow
    $MacX64Root = Join-Path $StagingDir "osx-x64/agy-cli-account-swarm-v$Version-macos-x64"
    $MacX64App = Join-Path $MacX64Root "Agy CLI Account Swarm.app"
    $MacX64Resources = Join-Path $MacX64App "Contents/Resources"
    $MacX64MacOS = Join-Path $MacX64App "Contents/MacOS"
    New-Item -ItemType Directory -Path $MacX64Resources -Force | Out-Null
    New-Item -ItemType Directory -Path $MacX64MacOS -Force | Out-Null

    dotnet publish $Csproj -c Release -r osx-x64 --self-contained false -o $MacX64Resources --nologo
    if ($LASTEXITCODE -ne 0) { throw "macOS x64 publish failed" }

    Write-MacPlist (Join-Path $MacX64App "Contents/Info.plist") $Version
    Write-MacLauncher (Join-Path $MacX64MacOS "agy-cli-account-swarm")

    # Copy installer scripts and uninstall helper
    $macX64Scripts = Join-Path $MacX64Root "scripts"
    New-Item -ItemType Directory -Path $macX64Scripts -Force | Out-Null
    Copy-Item (Join-Path $ScriptDir "install-macos.sh") (Join-Path $MacX64Root "install.sh") -Force
    Copy-Item (Join-Path $ScriptDir "install-macos.sh") (Join-Path $macX64Scripts "install-macos.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-macos.sh") (Join-Path $MacX64Root "uninstall.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-macos.sh") (Join-Path $macX64Scripts "uninstall-macos.sh") -Force

    $MacX64TarGz = Join-Path $DistDir "Agy-CLI-Account-Swarm-v$Version-macos-x64.tar.gz"
    Create-TarGz -ArchivePath $MacX64TarGz -WorkDir (Join-Path $StagingDir "osx-x64") -ItemName "agy-cli-account-swarm-v$Version-macos-x64"
    Write-Host "  [OK] Generated: Agy-CLI-Account-Swarm-v$Version-macos-x64.tar.gz" -ForegroundColor Green

    # --------------------------------------------------------------------------
    # [6/6] macOS ARM64 Apple Silicon (.app Bundle + .tar.gz)
    # --------------------------------------------------------------------------
    Write-Host ""
    Write-Host "[6/6] Compiling macOS ARM64 binaries & packaging .app bundle..." -ForegroundColor Yellow
    $MacArm64Root = Join-Path $StagingDir "osx-arm64/agy-cli-account-swarm-v$Version-macos-arm64"
    $MacArm64App = Join-Path $MacArm64Root "Agy CLI Account Swarm.app"
    $MacArm64Resources = Join-Path $MacArm64App "Contents/Resources"
    $MacArm64MacOS = Join-Path $MacArm64App "Contents/MacOS"
    New-Item -ItemType Directory -Path $MacArm64Resources -Force | Out-Null
    New-Item -ItemType Directory -Path $MacArm64MacOS -Force | Out-Null

    dotnet publish $Csproj -c Release -r osx-arm64 --self-contained false -o $MacArm64Resources --nologo
    if ($LASTEXITCODE -ne 0) { throw "macOS ARM64 publish failed" }

    Write-MacPlist (Join-Path $MacArm64App "Contents/Info.plist") $Version
    Write-MacLauncher (Join-Path $MacArm64MacOS "agy-cli-account-swarm")

    # Copy installer scripts and uninstall helper
    $macArmScripts = Join-Path $MacArm64Root "scripts"
    New-Item -ItemType Directory -Path $macArmScripts -Force | Out-Null
    Copy-Item (Join-Path $ScriptDir "install-macos.sh") (Join-Path $MacArm64Root "install.sh") -Force
    Copy-Item (Join-Path $ScriptDir "install-macos.sh") (Join-Path $macArmScripts "install-macos.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-macos.sh") (Join-Path $MacArm64Root "uninstall.sh") -Force
    Copy-Item (Join-Path $ScriptDir "uninstall-macos.sh") (Join-Path $macArmScripts "uninstall-macos.sh") -Force

    $MacArm64TarGz = Join-Path $DistDir "Agy-CLI-Account-Swarm-v$Version-macos-arm64.tar.gz"
    Create-TarGz -ArchivePath $MacArm64TarGz -WorkDir (Join-Path $StagingDir "osx-arm64") -ItemName "agy-cli-account-swarm-v$Version-macos-arm64"
    Write-Host "  [OK] Generated: Agy-CLI-Account-Swarm-v$Version-macos-arm64.tar.gz" -ForegroundColor Green
}

# ------------------------------------------------------------------------------
# Cleanup Staging
# ------------------------------------------------------------------------------
if (Test-Path $StagingDir) {
    Remove-Item -Path $StagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ------------------------------------------------------------------------------
# SHA256 Checksums
# ------------------------------------------------------------------------------
Write-Host ""
Write-Host "Computing SHA256 Checksums for all distribution packages..." -ForegroundColor Yellow
$ChecksumFile = Join-Path $DistDir "SHA256SUMS.txt"
$distFiles = Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -ne "SHA256SUMS.txt" }

$lines = [System.Collections.Generic.List[string]]::new()
foreach ($file in $distFiles) {
    $hash = (Get-FileHash -Path $file.FullName -Algorithm SHA256).Hash.ToLower()
    $lines.Add("$hash  $($file.Name)")
}
[System.IO.File]::WriteAllLines($ChecksumFile, $lines)
Write-Host "  [OK] Saved SHA256 checksums to: $ChecksumFile" -ForegroundColor Green

Write-Host ""
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host " Release v$Version Distribution Artifacts:" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan
Get-ChildItem -Path $DistDir | Format-Table Name, @{Label="Size (MB)"; Expression={"{0:N2} MB" -f ($_.Length / 1MB)}}, LastWriteTime
