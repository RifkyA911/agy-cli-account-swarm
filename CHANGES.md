# Laporan Perubahan (CHANGES.md) — Agy CLI Account Swarm

Laporan ini merinci seluruh audit keamanan, arsitektur, dan perbaikan menyeluruh yang dilakukan pada repositori **Agy CLI Account Swarm** (.NET 9 WPF) sesuai fase kerja.

---

## 1. Ringkasan Eksekutif

Semua 19 temuan yang diidentifikasi pada `AUDIT.md` (5 Critical, 7 High, 4 Medium, 3 Low) telah berhasil diperbaiki, diuji, dan diverifikasi secara menyeluruh.

- **Status Kompilasi (`dotnet build -c Release`)**: **0 Warning(s), 0 Error(s)**
- **Status Publikasi (`dotnet publish -c Release -o publish`)**: **0 Warning(s), 0 Error(s)**
- **Status Unit Test (`dotnet test -c Release`)**: **56 Passed, 0 Failed, 0 Skipped** (100% Green)

---

## 2. Rincian Perubahan Berdasarkan Kategori & ID AUDIT.md

### A. Keamanan & Hardening Sandboxing (SEC-01 s/d SEC-08)

| ID | Komponen | Perubahan | Rationale / Kenapa Diubah |
|---|---|---|---|
| **SEC-01** | `Services/TerminalLauncherService.cs` | Penambahan method `SanitizeBatchString` pada argumen batch launcher (`run-agy.cmd`). Strip karakter khusus batch `&`, `\|`, `<`, `>`, `^`, `"`, `%`. | Mencegah arbitrary command injection via nama profil atau path workspace saat terminal diluncurkan. |
| **SEC-02** | `Services/TerminalLauncherService.cs` | Sanitasi karakter titik koma (`;`) dan kutip ganda pada parameter Windows Terminal di `BuildWindowsTerminalArguments`. | Pada `wt.exe`, titik koma adalah delimiter subperintah. Sanitasi mencegah pemisahan parameter (parameter splitting) dan injeksi subperintah terminal. |
| **SEC-03** | `Services/TerminalLauncherService.cs` | Eksekusi script PowerShell kini menggunakan `-EncodedCommand` (Base64 UTF-16) menggantikan interpolasi `-Command "..."`. Penambahan `SanitizeCommandLineArgs` dan whitelist `SanitizeSessionArgs`. | Menghilangkan celah evaluasi dinamis kutip ganda/backtick di PowerShell CLI. |
| **SEC-04** | `Models/AccountProfile.cs` | Sanitasi nama folder `SanitizeFolderName` menyaring `..`, `/`, dan `\`. Path kanonikal divalidasi via `Path.GetFullPath()`, serta dilarang mengarah ke root drive atau folder Windows/System. | Mencegah path traversal keluar dari direktori sandbox profil (`~/.gemini-profiles/`). |
| **SEC-05** | `Services/DataProtectionService.cs`, `Services/AuthDetectorService.cs` | Implementasi Windows Data Protection API (DPAPI) via native Win32 `crypt32.dll` (`CryptProtectData`/`CryptUnprotectData`) dengan scope `CurrentUser`. Ditambahkan migrasi otomatis plain-text token saat dibaca. | Melindungi token OAuth Google (`antigravity-oauth-token`) at-rest tanpa dependensi NuGet eksternal. |
| **SEC-06** | `Services/Logger.cs`, `ViewModels/MainViewModel.cs` | Penambahan regex redaction filter pada `Logger.RedactSensitive` untuk menyaring JWT (`id_token`), token bearer, dan oauth token. Menghapus hardcoded email developer (`rifkyakhmad911@gmail.com`) pada laporan PDF/HTML. | Mencegah kebocoran kredensial ke disk (`app.log`) dan kebocoran data pribadi (PII) ke laporan ekspor. |
| **SEC-07** | `tests/AgyAccountSwarm.Tests/TerminalLauncherTests.cs` | Menghapus pembacaan Windows Credential Manager mesin asli (`ReadWindowsCredential`) dan path lokal developer dari test suite. Menggantinya dengan mock hermetik dan injection tests. | Test menjadi 100% hermetik, aman dijalankan di CI publik, dan tidak mengekspos kredensial pengguna. |
| **SEC-08** | `Services/ProfileStorageService.cs`, `Services/McpService.cs` | Menghapus referensi path hardcoded `C:\Users\rifky` dan `D:\Works`, menggantinya dengan `Environment.SpecialFolder.UserProfile` dan `IsMainDefaultProfile()`. | Memastikan aplikasi berfungsi konsisten pada lingkungan mesin pengguna mana pun. |

---

### B. Telemetri Autentik & Kuota Dinamis (TEL-01, TEL-02)

| ID | Komponen | Perubahan | Rationale / Kenapa Diubah |
|---|---|---|---|
| **TEL-01** | `Services/QuotaConfigService.cs`, `ViewModels/ProfileEditViewModel.cs`, `Services/AuthDetectorService.cs` | Membuat `QuotaConfigService` yang memuat konfigurasi kuota per-tier dari `%APPDATA%\AgyAccountSwarm\quota_config.json` (dibuat otomatis dengan default resmi bila belum ada). Seluruh static helper mendelegasikan ke service ini. | Menghentikan kuota hardcoded di kode C#. Pengguna dapat menyesuaikan batas harian/mingguan/token jika Google memperbarui kapasitas tier tanpa perlu compile ulang. |
| **TEL-02** | `Services/AgyUsageParser.cs`, `Services/AuthDetectorService.cs` | Mengintegrasikan eksekusi dan parsing `agy -p "/usage" --output-format json` secara defensif dengan caching thread-safe (TTL 60 detik). Mengekstrak bucket `gemini-weekly`, `gemini-5h`, `3p-weekly`, `3p-5h`, `remaining_fraction`, dan `reset_time` (ISO-8601 UTC). | Menggantikan estimasi tiruan dengan telemetri 100% autentik langsung dari output resmi CLI agy tanpa memakan kuota token percakapan (`num_turns = 0`). |

---

### C. Stabilitas, Resource & Error Handling (ERR-01, ERR-02, PERF-01, PERF-02)

| ID | Komponen | Perubahan | Rationale / Kenapa Diubah |
|---|---|---|---|
| **ERR-01** | `ViewModels/MainViewModel.cs`, `Views/MainWindow.xaml.cs` | Membungkus `OnAutoSyncTimerTick` dan event context menu tray dengan `try/catch` komprehensif dan `Logger.Error`. Menghilangkan `async void` lambda yang tidak aman. | Mencegah crash fatal proses WPF (CLR unhandled exception) jika background timer atau operasi async mengalami disk I/O lock. |
| **ERR-02** | `Services/AuthDetectorService.cs`, `Services/TelemetryService.cs`, `Services/McpService.cs`, `ViewModels/MainViewModel.cs` | Mengganti belasan blok `catch { }` kosong dengan logging diagnostik informatif (`Logger.Warn` atau `Logger.Debug`). | Menghilangkan *silent failure*; kegagalan pembacaan baris log atau parsing setting dapat dilacak melalui tab `/logs`. |
| **PERF-01** | `Services/AuthDetectorService.cs` | Mengganti `File.ReadAllLines` di `GetAvailableSessions` dengan streaming `StreamReader` menggunakan `FileShare.ReadWrite`. | Mencegah lonjakan alokasi memori (RAM) saat file `history.jsonl` berukuran puluhan/ratusan megabyte, serta menghindari file locking exception saat agy sedang aktif menulis history. |
| **PERF-02** | `Services/Logger.cs` | Mengganti `List<string>.RemoveAt(0)` dengan `Queue<string>.Dequeue()` $O(1)$ pada memory ring buffer logger. | Mengeliminasi memory shift copy $O(N)$ saat ring buffer 1.000 baris mencapai kapasitas penuh, meningkatkan throughput logging. |

---

### D. UI & Ergonomi (UI-01)

| ID | Komponen | Perubahan | Rationale / Kenapa Diubah |
|---|---|---|---|
| **UI-01** | `Views/MainWindow.xaml`, `Views/MainWindow.xaml.cs` | Menambahkan event handler `AccountsScrollViewer_PreviewMouseWheel` pada `ScrollViewer` kartu akun. | Menjawab keluhan pengguna ("*scrollnya kok kecepeten ya*"). Standar mouse wheel WPF meloncat kasar (~144px per notch); handler meredam scroll delta menjadi 50px per notch untuk pergerakan kartu akun yang halus dan presisi. |

---

### E. Dokumentasi, Kepatuhan & Workflow CI/CD (REL-01, DOC-01, CI-01, CI-02)

| ID | Komponen | Perubahan | Rationale / Kenapa Diubah |
|---|---|---|---|
| **REL-01** | `README.md`, `scripts/install-linux.sh`, `scripts/install-macos.sh`, `.github/workflows/release.yml` | `README.md` ditulis ulang secara minimalis, faktual, tanpa kata-kata marketing slop. Menghapus klaim palsu dukungan native Linux/macOS. Packaging tarball Linux/macOS yang rusak dihapus dari release workflow. Wrapper script Linux/macOS kini secara eksplisit mewajibkan Wine dan menolak eksekusi `dotnet dll` yang dipastikan crash. | Melindungi pengguna dari mengunduh binary WPF yang tidak kompatibel di OS non-Windows. |
| **DOC-01** | `SECURITY.md`, `README.md` | Menambahkan Threat Model 4 pilar di `SECURITY.md` (DPAPI at-rest, command injection defense, path traversal isolation, telemetry provenance). Menambahkan Disclaimer Google Terms of Service (ToS) dan panduan troubleshooting di `README.md`. | Transparansi arsitektur keamanan dan kepatuhan hukum bagi pengguna multi-akun. |
| **CI-01** | `.github/workflows/build.yml` | Menambahkan step `dotnet test --configuration Release --no-build --verbosity normal` pada workflow PR/push. | Memastikan setiap pull request dan push ke master wajib lolos seluruh unit test sebelum di-merge. |
| **CI-02** | `.github/workflows/build.yml`, `.github/workflows/release.yml` | Seluruh GitHub Actions pihak ketiga (`actions/checkout`, `actions/setup-dotnet`, `softprops/action-gh-release`) di-pin ke full 40-character commit SHA immutable. Minimalisasi token permissions. | Mencegah supply chain attack akibat modifikasi tag rilis upstream GitHub Actions. |

---

## 3. Apa yang Sengaja Tidak Diubah (Rasionalitas)

1. **Arsitektur WPF .NET 9 Tetap Dipertahankan (Tidak Di-rewrite ke Avalonia Sekarang)**:
   - *Alasan*: Repo ini adalah aplikasi desktop Windows yang kaya akan animasi XAML, visual tree custom chrome, interop Windows Terminal, dan System Tray Windows Forms. Rewrite ke Avalonia membutuhkan refactor total pada layer Views dan bindings yang berada di luar lingkup perbaikan audit keamanan dan telemetri.
2. **Tidak Menambah Library Eksternal / NuGet Baru**:
   - *Alasan*: Implementasi DPAPI dilakukan langsung melalui P/Invoke `crypt32.dll` Windows tanpa menambah dependensi `System.Security.Cryptography.ProtectedData` pihak ketiga, menjaga binary tetap ramping dan terisolasi dari potensi supply chain vulnerability.
3. **Format history.jsonl agy Asli Tetap Dibaca Read-Only**:
   - *Alasan*: Aplikasi ini bertindak sebagai sandbox orchestrator yang menghormati data asli CLI Google Antigravity tanpa memodifikasi schema history internal CLI agar tidak merusak kompatibilitas backward/forward.

---

## 4. Risiko yang Masih Tersisa (Residual Risks)

1. **Perubahan Format JSON / Output CLI agy di Masa Depan**:
   - Jika Google memperbarui `agy` dan mengubah skema `command.data.groups[].buckets[]` pada `/usage`, `AgyUsageParser` dirancang defensif sehingga tidak akan crash, melainkan akan fallback secara anggun ke perhitungan estimasi lokal dari `history.jsonl`.
2. **Keterbatasan Lingkungan Non-Windows (Wine)**:
   - Meskipun installer script menyediakan mode Wine untuk Linux/macOS, WPF memerlukan Wine dengan dukungan Direct3D/GDI+ yang memadai. Pengalaman pengguna terbaik tetaplah di Windows 10/11 secara native.

---

## 5. Langkah Manual untuk Pengguna / Maintainer

Sebelum merilis versi publik atau memperbarui installer:
1. **Code Signing Certificate (Opsional tapi Disarankan)**:
   - Jika memiliki sertifikat Authenticode (EV/OV), tandatangani file `publish/AgyAccountSwarm.exe` dan `dist/Agy-CLI-Account-Swarm-Setup-v*.exe` menggunakan `signtool.exe` untuk menghindari peringatan Windows SmartScreen.
2. **Konfigurasi Kuota Lokal**:
   - File konfigurasi kuota berada di `%APPDATA%\AgyAccountSwarm\quota_config.json`. Anda dapat mengubah batas kuota per tier kapan saja jika Google memberikan tier promosi atau perubahan kapasitas.
3. **GitHub Release Tagging**:
   - Saat siap merilis versi baru ke GitHub:
     ```powershell
     git tag v0.9.4-beta
     git push origin v0.9.4-beta
     ```
   - Workflow `.github/workflows/release.yml` yang telah diperbaiki akan otomatis mengompilasi installer Inno Setup, memaketkan ZIP portabel, menghitung SHA256 checksums, dan menerbitkan GitHub Release.

---

## 6. Bukti Verifikasi Teknis

### A. `dotnet build --configuration Release`
```
  Determining projects to restore...
  All projects are up-to-date for restore.
  AgyAccountSwarm -> D:\Works\Project\C#\agy-cli-account-swarm\bin\Release\net9.0-windows\AgyAccountSwarm.dll
  AgyAccountSwarm.Tests -> D:\Works\Project\C#\agy-cli-account-swarm\tests\AgyAccountSwarm.Tests\bin\Release\net9.0-windows\AgyAccountSwarm.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### B. `dotnet test --configuration Release`
```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    56, Skipped:     0, Total:    56, Duration: 10 s - AgyAccountSwarm.Tests.dll (net9.0)
```

### C. `dotnet publish AgyAccountSwarm.csproj -c Release -o publish --nologo`
```
  Determining projects to restore...
  All projects are up-to-date for restore.
  AgyAccountSwarm -> D:\Works\Project\C#\agy-cli-account-swarm\bin\Release\net9.0-windows\AgyAccountSwarm.dll
  AgyAccountSwarm -> D:\Works\Project\C#\agy-cli-account-swarm\publish\

Build succeeded.
    0 Warning(s)
    0 Error(s)
```
