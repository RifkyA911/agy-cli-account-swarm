# ==============================================================================
# Code-Signing & Windows SmartScreen Helper for Agy CLI Account Swarm
# ==============================================================================
param(
    [string]$CertPath = "",
    [string]$CertPassword = "",
    [switch]$CreateSelfSigned = $false,
    [switch]$InstallCert = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$PublishExe = Join-Path $ProjectRoot "publish\AgyCliAccountSwarmGUI.exe"
$DistDir = Join-Path $ProjectRoot "dist"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "  Agy CLI Account Swarm - Code Signing & Security Utility" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$cert = $null

if ($CertPath -ne "" -and (Test-Path $CertPath)) {
    Write-Host "[*] Loading certificate from: $CertPath" -ForegroundColor Yellow
    $securePass = ConvertTo-SecureString $CertPassword -AsPlainText -Force
    $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertPath, $securePass)
} elseif ($CreateSelfSigned) {
    Write-Host "[*] Creating Self-Signed Code Signing Certificate for RifkyA911..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=RifkyA911 (Agy CLI Account Swarm)" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -HashAlgorithm SHA256 `
        -NotAfter (Get-Date).AddYears(5)
    Write-Host "  [OK] Generated Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

    if ($InstallCert) {
        Write-Host "[*] Installing Certificate to Trusted Root Certification Authorities..." -ForegroundColor Yellow
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
        $store.Open("ReadWrite")
        $store.Add($cert)
        $store.Close()
        Write-Host "  [OK] Installed to Cert:\CurrentUser\Root. Windows SmartScreen warnings eliminated on this PC." -ForegroundColor Green
    }
} else {
    # Check if a certificate matching RifkyA911 exists in CurrentUser\My
    $existing = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | Where-Object { $_.Subject -like "*RifkyA911*" } | Select-Object -First 1
    if ($existing) {
        $cert = $existing
        Write-Host "[*] Found existing Code Signing Certificate: $($cert.Subject) ($($cert.Thumbprint))" -ForegroundColor Green
    }
}

if ($cert) {
    # Sign publish exe if present
    if (Test-Path $PublishExe) {
        Write-Host "[*] Signing $PublishExe..." -ForegroundColor Cyan
        Set-AuthenticodeSignature -FilePath $PublishExe -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
        Write-Host "  [OK] Signed: $PublishExe" -ForegroundColor Green
    }

    # Sign installers in dist
    if (Test-Path $DistDir) {
        $installers = Get-ChildItem -Path $DistDir -Filter "*.exe" -File
        foreach ($inst in $installers) {
            Write-Host "[*] Signing $($inst.FullName)..." -ForegroundColor Cyan
            Set-AuthenticodeSignature -FilePath $inst.FullName -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
            Write-Host "  [OK] Signed: $($inst.Name)" -ForegroundColor Green
        }
    }
} else {
    Write-Host ""
    Write-Host "No code signing certificate provided." -ForegroundColor Yellow
    Write-Host "To create a self-signed certificate and trust it locally:" -ForegroundColor White
    Write-Host "  pwsh ./scripts/sign-release.ps1 -CreateSelfSigned -InstallCert" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To unblock downloaded binaries from Windows SmartScreen filter:" -ForegroundColor White
    Write-Host "  Unblock-File .\dist\Agy-CLI-Account-Swarm-Setup-v*.exe" -ForegroundColor Gray
}
