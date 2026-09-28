# Laporan Audit Keamanan, Kualitas & Arsitektur
# Agy CLI Account Swarm (v0.9.3-beta)

**Tanggal Audit**: 28 September 2026  
**Auditor**: Senior C#/.NET & Security Engineer  
**Target Repository**: `RifkyA911/agy-cli-account-swarm`  
**Target Framework**: .NET 9.0 (WPF, Windows Desktop)  
**Status Audit**: Fase 1 Selesai (Temuan Empiris & Rekomendasi)

---

## 1. Tabel Temuan Audit

| ID | Severity | File:Baris | Masalah | Dampak | Saran Fix | Status |
|---|---|---|---|---|---|---|
| **SEC-01** | **Critical** | `Services/TerminalLauncherService.cs:70-91` | **Command Injection pada Generator Script Batch (`run-agy.cmd`)**: Variabel `profile.Name`, `effectiveDir`, dan `workDir` ditempel langsung ke dalam file batch tanpa sanitasi karakter khusus CMD (`&`, `\|`, `<`, `>`, `^`, `"`, `%`). | Akun dengan nama seperti `Worker & calc.exe` atau payload injeksi dalam nama profil/workspace akan mengeksekusi perintah arbitrer saat profil diluncurkan. | Terapkan validasi whitelist karakter nama profil (`^[a-zA-Z0-9_\-\.\s]+$`), sanitasi batch argument, dan tulis file dengan encoding UTF-8 BOM/UTF-8. | **Fixed** (Sanitasi batch argument `SanitizeBatchString`, strip karakter pemisah & command chaining, UTF8 encoding) |
| **SEC-02** | **Critical** | `Services/TerminalLauncherService.cs:101-105, 223, 227` | **Command Injection & Parameter Splitting di Windows Terminal (`wt.exe`)**: Pada `wt.exe`, karakter titik koma (`;`) adalah pemisah subperintah (*subcommand delimiter*). Argumen `title`, `workingDir`, dan `scriptPath` disisipkan tanpa escaping terhadap kutip (`"`) dan titik koma (`;`). | Jika nama profil atau path workspace mengandung `;` atau `"`, `wt.exe` akan menginterpretasikannya sebagai subperintah baru dan mengeksekusi proses yang tidak diinginkan. | Escape kutip ganda dan sanitasi karakter delimiter `;` pada seluruh parameter `wt.exe`. | **Fixed** (Sanitasi delimiter `;` dan kutip di `BuildWindowsTerminalArguments` dan penanganan `wtArgs`) |
| **SEC-03** | **Critical** | `Services/TerminalLauncherService.cs:106-126, 170` | **Command Injection di PowerShell Launcher**: Eksekusi PowerShell menggunakan `-Command "{psScript}"` di dalam string berkutip ganda. Di PowerShell, ekspresi di dalam double quotes dievaluasi secara dinamis. Parameter `extraArgs` juga ditempel mentah tanpa sanitasi. | Input argumen tambahan atau manipulasi path yang mengandung kutip/dollar/backtick dapat mengeksekusi kode PowerShell arbitrer. | Ganti `-Command` dengan `-EncodedCommand` (Base64) atau gunakan file script sementara/eksekusi parameter terpisah tanpa string concatenation. Validasi `extraArgs`. | **Fixed** (Menggunakan `-EncodedCommand` Base64 UTF-16, sanitasi argumen ekstra `SanitizeCommandLineArgs`, dan whitelist `SanitizeSessionArgs`) |
| **SEC-04** | **Critical** | `Models/AccountProfile.cs:61-69, 78-83` | **Path Traversal pada Direktori Profil Sandbox**: `SanitizeFolderName` hanya memeriksa `Path.GetInvalidFileNameChars()`, yang tidak menyaring titik ganda (`..`). Selain itu, `CustomProfilePath` langsung di-expand dengan `Environment.ExpandEnvironmentVariables` tanpa kanonikalisasi (`Path.GetFullPath`) dan tanpa memverifikasi apakah path berada di dalam batas yang aman. | Profil dengan nama seperti `../../Windows/System32` atau `CustomProfilePath` sembarang dapat mengarahkan operasi baca/tulis/kloning ke direktori sensitif sistem. | Batasi `CustomProfilePath` ke direktori yang valid dan terotorisasi; sanitasi nama profil dari urutan traversal (`..`, `./`, `.\`), serta validasi canonical path menggunakan `Path.GetFullPath()`. | **Fixed** (Penyaringan traversal `..` di `SanitizeFolderName`, kanonikalisasi `Path.GetFullPath`, dan proteksi direktori sistem/root drive) |
| **SEC-05** | **Critical** | `Services/AuthDetectorService.cs:64-75`, `Services/ProfileStorageService.cs:26, 74-80` | **Penyimpanan Token & Kredensial Tidak Terenkripsi (Plain-text at-rest)**: Token OAuth (`antigravity-oauth-token`) dan metadata profil disimpan sebagai plain JSON di filesystem tanpa enkripsi OS (DPAPI) dan tanpa proteksi ACL direktori. | Malware atau aplikasi non-elevated lain di user session yang sama dapat mencuri token OAuth Google pengguna tanpa halangan. | Enkripsi token dan credential cache menggunakan Windows DPAPI (`ProtectedData.Protect` dengan scope `DataProtectionScope.CurrentUser`), dan set folder ACL hanya untuk pengguna aktif. | **Fixed** (`DataProtectionService` menggunakan Windows DPAPI CryptProtectData/CryptUnprotectData dengan migrasi transparan otomatis dari plain token) |
| **SEC-06** | **High** | `Services/Logger.cs:48-52, 95-115`, `ViewModels/MainViewModel.cs:1861` | **Kebocoran Data Sensitif & PII (Hardcoded Email & Token Logging)**: Email pribadi developer (`rifkyakhmad911@gmail.com`) di-hardcode pada laporan PDF/HTML. Selain itu, `Logger` tidak memiliki mekanisme sanitasi/redaksi terhadap token (`access_token`, `id_token`, `Bearer`), sehingga exception atau pesan debug berpotensi mencatat token ke disk (`app.log`). | Terjadinya kebocoran data pribadi (PII) ke laporan yang diekspor dan kredensial login ke file log. | Hapus hardcoded email pribadi, ganti dengan operator email dinamis dari profil aktif. Tambahkan token redaction regex filter pada `Logger.Write`. | **Fixed** (Pembersihan email pribadi di laporan PDF/HTML dan penambahan regex redaction filter untuk token JWT/OAuth di `Logger.RedactSensitive`) |
| **SEC-07** | **High** | `tests/AgyAccountSwarm.Tests/TerminalLauncherTests.cs:196-202, 237-252` | **Kebocoran Identitas Asli & Pelanggaran Hermetic Test**: Unit test membaca Windows Credential Manager mesin asli (`ReadWindowsCredential("gemini:antigravity")`) dan hardcoded path profil lokal developer (`C:\Users\rifky\...`) beserta email asli pengguna. | Test gagal di lingkungan CI bersih (GitHub Actions) dan membocorkan data pribadi developer ke public repository. | Hapus akses kredensial riil dari test suite; ganti 100% dengan hermetic mock filesystem dan dummy data. | **Fixed** (Menghapus test yang membaca Credential Manager asli & hardcoded user paths; menggantinya dengan mock data & injection tests) |
| **SEC-08** | **High** | `Services/ProfileStorageService.cs:56`, `Services/McpService.cs:120` | **Path Mesin Lokal Developer di-Hardcode di Kode Produksi**: Ditemukan path spesifik lokal developer (`C:\Users\rifky` dan `D:\Works`) di dalam logic runtime aplikasi. | Menimbulkan perilaku tidak konsisten pada mesin pengguna lain (misalnya menganggap path orang lain tidak valid atau mengarah ke path yang tidak ada). | Hapus seluruh referensi path hardcoded personal; gunakan `Environment.SpecialFolder` dan konfigurasi relatif pengguna. | **Fixed** (Menghapus referensi `C:\Users\rifky` dan `D:\Works`, menggantinya dengan `IsMainDefaultProfile()` dan `Environment.SpecialFolder.UserProfile`) |
| **TEL-01** | **High** | `Services/AuthDetectorService.cs:680-711`, `ViewModels/ProfileEditViewModel.cs:148-155` | **Tabel Kuota & Limit Model Di-Hardcode Statis**: Kuota tier (Basic=100, Plus=300, Pro=1000, Ultra=2500) dan limit token per tier di-hardcode ganda di kode C#. Data kuota sesungguhnya tidak diambil dari CLI. | Pengguna tidak dapat menyesuaikan kuota saat Google mengubah aturan tier, dan tampilan kuota tidak mencerminkan data aktual dari server jika tier akun berbeda. | Pindahkan tabel kuota ke file konfigurasi `quota_config.json` yang dapat diedit pengguna (dengan nilai default), dan manfaatkan integrasi `agy -p "/usage" --output-format json` untuk telemetri autentik. | **Fixed** (`QuotaConfigService` memindahkan limit tier ke `%APPDATA%\AgyAccountSwarm\quota_config.json` yang dapat dikonfigurasi dinamis, didelegasikan oleh `AuthDetectorService` dan `ProfileEditViewModel`) |
| **TEL-02** | **High** | `Services/AuthDetectorService.cs:342-363`, `ViewModels/MainViewModel.cs:1261-1270` | **Perhitungan Telemetri Asumtif (Formula Tiruan)**: Kuota mingguan dan rolling 5-hour dihitung menggunakan asumsi lokal (Sunday midnight dan rolling offset tebakan), serta estimasi token menggunakan konstanta pengali semata (`* 1850L`). | Data sisa kuota dan waktu reset sering meleset dari sisa kuota nyata yang dilaporkan oleh Google Antigravity. | Gunakan data autentik dari `agy -p "/usage" --output-format json` yang menyajikan `reset_time` resmi (ISO-8601 UTC) dan `remaining_fraction` riil. | **Fixed** (`AgyUsageParser` mengeksekusi `agy -p "/usage" --output-format json` secara defensif dengan caching thread-safe, parsing bucket Gemini & Claude/GPT dengan countdown ISO-8601 UTC asli) |
| **REL-01** | **High** | `README.md:178-224`, `scripts/install-linux.sh`, `scripts/install-macos.sh`, `.github/workflows/release.yml:60-78` | **Klaim Palsu / Menyesatkan Dukungan Cross-Platform Linux & macOS**: README dan workflow merilis paket installer Linux dan macOS, padahal aplikasi adalah WPF .NET 9 (`UseWPF=true`, `net9.0-windows`) yang **Windows-only**. Script wrapper mencoba memanggil `dotnet AgyAccountSwarm.dll` di Linux yang dipastikan crash (`PlatformNotSupportedException`). | Pengguna Linux/macOS mengunduh paket yang tidak bisa berjalan dan mengalami crash fatal. | Koreksi README: nyatakan secara tegas bahwa saat ini aplikasi adalah **Windows Only**; tandai dukungan Linux/macOS sebagai planned (Avalonia). Hapus packaging non-fungsional dari release workflow. | Open |
| **ERR-01** | **High** | `ViewModels/MainViewModel.cs:1674`, `MainWindow.xaml.cs:77` | **Penggunaan `async void` pada Event Handlers**: `OnAutoSyncTimerTick` dan event menu tray dideklarasikan sebagai `async void`. | Jika terjadi unhandled exception pada background sync (misal disk I/O failure atau network issue), CLR akan langsung meng-crash seluruh proses WPF tanpa tertangkap oleh `DispatcherUnhandledException`. | Ganti dengan async method bertipe `Task` yang dibungkus `try/catch` komprehensif, atau gunakan safe async event command wrapper. | **Fixed** (`OnAutoSyncTimerTick` dibungkus try/catch komprehensif dengan `Logger.Error`, dan event tray diganti dengan safe task runner dengan global error logger) |
| **ERR-02** | **Medium** | Berbagai file (`Services/TelemetryService.cs:113, 119`, `Services/AuthDetectorService.cs:58, 116, 279, 555`, `ViewModels/MainViewModel.cs:579, 1054, 1061, 1066, 1073, 1089`) | **Exception Ditelan Mentah-Mentah Tanpa Logging (`catch { }` Kosong)**: Terdapat belasan blok try-catch kosong yang menelan exception tanpa memanggil `Logger.Warn` atau `Logger.Error`. | Aplikasi gagal secara hening (silent failure). Jika parsing history rusak atau database terkunci, engineer tidak memiliki jejak diagnostik untuk perbaikan. | Tambahkan logging informatif minimal `Logger.Warn($"Failed to read ... : {ex.Message}")` dan skip record yang rusak secara aman. | **Fixed** (Seluruh empty catch blocks di `AuthDetectorService`, `TelemetryService`, `MainViewModel`, dan `McpService` digantikan dengan logging diagnostik informatif `Logger.Warn`/`Logger.Debug`) |
| **PERF-01** | **Medium** | `Services/AuthDetectorService.cs:511` | **Membaca Seluruh File History ke Memori Sekaligus (`File.ReadAllLines`)**: Method `GetAvailableSessions` membaca seluruh baris `history.jsonl` menggunakan `ReadAllLines`. | Jika pengguna telah menggunakan CLI selama berbulan-bulan dan file mencapai ratusan megabyte, UI akan mengalami stutter/lag parah dan lonjakan konsumsi RAM. | Ganti dengan streaming reader dari akhir file (reverse buffer stream) atau batasi pembacaan hanya pada N baris terakhir yang relevan. | **Fixed** (`GetAvailableSessions` membaca `history.jsonl` secara streaming menggunakan `FileStream` dengan `FileShare.ReadWrite` dan `StreamReader` baris-demi-baris tanpa membebani RAM) |
| **PERF-02** | **Medium** | `Services/Logger.cs:100-104` | **Inefisiensi Ring Buffer Logging (`List.RemoveAt(0)`)**: Log buffer menggunakan `List<string>` di mana `RemoveAt(0)` memicu memory array copy O(N) pada setiap baris log baru saat buffer penuh. | Menurunkan performa throughput logging saat swarm aktif menulis banyak output trace. | Ganti dengan `Queue<string>` atau circular ring-buffer array berukuran tetap O(1). | **Fixed** (Mengganti `List<string>.RemoveAt(0)` dengan `Queue<string>.Dequeue()` O(1)) |
| **UI-01** | **Medium** | `Views/MainWindow.xaml:1159`, `Views/MainWindow.xaml.cs` | **Kecepatan Scroll Terlalu Cepat di Tab Accounts**: `ScrollViewer` di tab Accounts menggunakan default WPF mouse wheel scrolling yang meloncat per item atau step delta besar. | Pengguna mengeluhkan scrolling terlalu sensitif dan melompat-lompat saat membaca kartu profil akun ("scrollnya kok kecepeten ya"). | Tambahkan handling `PreviewMouseWheel` yang halus atau atur `VirtualizingPanel.ScrollUnit="Pixel"` dengan delta step yang ergonomis. | **Fixed** (`AccountsScrollViewer_PreviewMouseWheel` meredam delta mouse wheel dengan rasio 50px per notch standar untuk scrolling kartu akun yang halus dan stabil) |

| **CI-01** | **Medium** | `.github/workflows/build.yml:25-27` | **CI Build Workflow Tidak Menjalankan Unit Tests**: Workflow `build.yml` hanya menjalankan `dotnet build` tanpa `dotnet test`. | Regresi kode atau test yang rusak dapat ter-merge ke branch `master` tanpa terdeteksi di Pull Request. | Tambahkan step `dotnet test --no-build --verbosity normal` pada `build.yml`. | Open |
| **CI-02** | **Low** | `.github/workflows/build.yml:15, 18`, `.github/workflows/release.yml:18, 21, 91` | **GitHub Actions Menggunakan Tag Mengambang Bukan Commit SHA**: Actions pihak ketiga (`actions/checkout@v4`, `actions/setup-dotnet@v4`, `softprops/action-gh-release@v2`) tidak di-pin ke SHA commit immutable. | Rentan terhadap supply chain attack jika tag rilis upstream dimodifikasi. | Pin seluruh GitHub Actions ke full commit SHA 40 karakter beserta komentar versi. | Open |
| **DOC-01** | **Low** | `SECURITY.md:1-15`, `README.md` | **Ketiadaan Threat Model & Disclaimer ToS**: `SECURITY.md` tidak menjelaskan batasan model keamanan (threat model), dan `README.md` tidak mencantumkan disclaimer terkait Google Terms of Service terkait multi-akun. | Pengguna tidak memahami batas proteksi sandbox lokal, serta risiko penggunaan multi-akun. | Perbarui `SECURITY.md` dengan threat model singkat dan sertakan disclaimer ToS yang transparan di `README.md`. | Open |

---

## 2. Analisis Mendalam Berdasarkan 12 Fokus Periksa

### 1. Keamanan Token
- **OAuth Token Storage**: Disimpan sebagai file plain text bernama `antigravity-oauth-token` di folder sandbox profil pengguna (`~/.gemini-profiles/{name}/.gemini/antigravity-cli/`). Tidak ada enkripsi simetris maupun Windows Data Protection API (DPAPI).
- **File / Folder Permissions**: Folder profil dibuat menggunakan `Directory.CreateDirectory` standar tanpa mengatur Security Descriptor Definition Language (SDDL) atau Windows Access Control List (ACL). Siapa pun yang memiliki hak akses lokal di mesin dapat membaca token tersebut.
- **Kebocoran Token**: `Logger.cs` tidak menyaring keyword sensitif. Bila terjadi exception pada saat token diproses, ada potensi payload token masuk ke `app.log`. Pada ekspor laporan HTML/PDF, email developer pribadi ter-hardcode secara statis (`rifkyakhmad911@gmail.com`).

### 2. Command / Argument Injection
- Generator script `run-agy.cmd` menyusun file batch menggunakan string interpolation langsung tanpa sanitasi karakter escape batch (`&`, `|`, `"`, `%`).
- Pemanggilan `wt.exe` menggabungkan argumen dengan pemisah titik koma (`;`), yang merupakan delimiter perintah internal Windows Terminal. Jika nama profil atau path berisi `;`, Windows Terminal akan mengeksekusi subperintah baru.
- Pemanggilan `powershell.exe` menggunakan parameter `-Command "{psScript}"` di mana kutip ganda menyebabkan evaluasi ekspresi dinamis PowerShell. Karakter `$`, backtick, atau kutip dapat memicu eksekusi kode tak terduga.
- Parameter `sessionArgs` (seperti `--conversation <id>`) disuntikkan tanpa memverifikasi bahwa `<id>` adalah UUID/hash yang aman.

### 3. Path Traversal
- `SanitizeFolderName` hanya membuang karakter dari `Path.GetInvalidFileNameChars()`. Karakter titik ganda (`..`) tidak disaring, sehingga nama profil seperti `../../Temp` berpotensi menulis script atau folder di luar root direktori `.gemini-profiles`.
- Nilai `CustomProfilePath` langsung di-expand dengan `Environment.ExpandEnvironmentVariables` tanpa normalisasi `Path.GetFullPath()` dan tanpa pengecekan apakah folder tersebut berada di lokasi yang diperbolehkan.

### 4. Ketergantungan ke Internal agy
- Aplikasi sangat bergantung pada trik variabel lingkungan `SSH_CONNECTION=1` dan `SSH_CLIENT=1` untuk mematikan Windows Credential Manager fallback pada profile worker terisolasi. Jika arsitektur `agy` di masa depan mengubah logika keyring detector, isolasi akun sekunder bisa terganggu.
- Aplikasi membaca langsung file `history.jsonl`, `settings.json`, dan `antigravity-oauth-token` milik internal CLI. Saat ini tidak ada pengecekan versi `agy` saat runtime untuk memvalidasi apakah format file yang dibaca masih kompatibel.

### 5. Akurasi Telemetri
- **Fakta Terbukti**: `agy -p "/usage" --output-format json` sukses dieksekusi di terminal sungguhan (agy versi 1.2.12), menghasilkan data kuota resmi dari Google Antigravity dengan `num_turns = 0`, durasi ~6 detik, dan konsumsi token 0.
- **Kelemahan Saat Ini**: Kode aplikasi masih menggunakan kalkulasi tabel kuota statis yang di-hardcode (Basic=100, Plus=300, Pro=1000, Ultra=2500) dan formula countdown lokal (asumsi reset Sunday midnight), bukannya mengambil data dari config atau output resmi CLI.

### 6. Concurrency dan Resource
- Timer auto-sync `_autoSyncTimer` menggunakan signature `async void` pada event handler `OnAutoSyncTimerTick`. Jika operasi asynchronous di dalamnya throw exception, proses akan crash seketika.
- Pembuatan task background menggunakan `_ = Task.Run(...)` tanpa pengawasan dan tanpa `CancellationToken`, sehingga task tetap berjalan di background saat aplikasi ditutup.
- Download avatar pengguna dilakukan secara paralel tanpa sinkronisasi file-write, berpotensi memicu `IOException: The process cannot access the file because it is being used by another process`.
- `RecentBuffer.RemoveAt(0)` pada `Logger` memiliki kompleksitas O(N) yang membebani CPU saat jumlah baris log tinggi.

### 7. Error Handling
- Ditemukan lebih dari 15 blok `catch { }` kosong yang menelan error secara hening (silent failures). Bila format history berubah atau ada file yang corrupt, aplikasi tidak memberikan catatan apapun di log.
- Penanganan URL eksternal di `OpenUrl` menelan semua error tanpa memberi notifikasi ke pengguna jika browser default gagal terbuka.

### 8. Kualitas Kode MVVM
- Terdapat duplikasi logika perhitungan kuota tier antara `AuthDetectorService` dan `ProfileEditViewModel`.
- Nilai-nilai magic strings dan magic numbers tersebar luas di view model.
- Ditemukan path hardcoded mesin developer (`C:\Users\rifky`, `D:\Works`) di kode produksi.
- Masalah scrolling mouse wheel di halaman Accounts yang terlalu sensitif belum teratasi secara ergonomis.

### 9. Klaim README vs Kenyataan
- README mengklaim aplikasi dapat diinstal dan dijalankan di Linux dan macOS (`scripts/install-linux.sh`, `scripts/install-macos.sh`). Ini klaim yang keliru karena aplikasi dibangun dengan WPF .NET 9 yang hanya berjalan di Windows.
- Klaim "Authentic Telemetry" belum sepenuhnya terpenuhi karena kuota harian dan batas token masih dihitung dari tabel statis internal, bukan konfigurasi dinamis atau CLI output resmi.
- Belum ada disclaimer mengenai Google Terms of Service terkait kepemilikan dan penggunaan multi-akun secara bersamaan.

### 10. Build dan Release
- Workflow `build.yml` tidak menjalankan `dotnet test`.
- Action workflow tidak di-pin ke immutable commit SHA.
- Packaging Linux dan macOS di `release.yml` membungkus binary Windows WPF yang tidak dapat dijalankan di target OS tersebut.

### 11. Test Coverage
- Unit test belum mencakup:
  1. Keamanan escaping karakter khusus dan command injection pada `TerminalLauncherService`.
  2. Proteksi path traversal pada `AccountProfile.GetEffectiveProfileDirectory()`.
  3. Ketahanan `TelemetryService` terhadap baris JSONL yang rusak atau tidak lengkap.
  4. Penyimpanan dan pembacaan konfigurasi di `ProfileStorageService`.
- Sejumlah test saat ini bergantung pada kredensial lokal dan file sistem mesin developer, bukan hermetic mock.

### 12. Supply Chain dan Lisensi
- Paket NuGet `CommunityToolkit.Mvvm` versi 8.4.2 memiliki lisensi MIT dan bebas dari kerentanan yang diketahui (`dotnet list package --vulnerable` = 0 vulnerability).
- Lisensi proyek adalah MIT, kompatibel dengan seluruh library yang digunakan.

---

## 3. (a) 5 Risiko Terbesar

1. **Remote/Local Command Injection via Profile & Workspace Parameters (SEC-01, SEC-02, SEC-03)**:
   Karakter khusus pada nama profil, workspace path, atau extra arguments dapat disalahgunakan untuk mengeksekusi perintah CMD, PowerShell, atau Windows Terminal di luar kendali pengguna.
2. **Plain-Text Token Storage & Ketiadaan Access Control (SEC-05)**:
   OAuth token tersimpan telanjang tanpa enkripsi DPAPI pada direktori tanpa pembatasan ACL pengguna.
3. **Klaim Palsu Dukungan Linux/macOS yang Merusak Kredibilitas Proyek (REL-01)**:
   Menyediakan script instalasi Linux/macOS untuk aplikasi WPF Windows murni menciptakan pengalaman buruk (crash langsung) bagi pengguna non-Windows.
4. **Crash Proses Mendadak Akibat `async void` pada Auto-Sync Timer (ERR-01)**:
   Unhandled exception pada background timer tick tidak dapat ditangkap oleh `DispatcherUnhandledException` dan langsung mematikan aplikasi.
5. **Kebocoran Identitas Asli Developer di Public Code & Laporan PDF (SEC-06, SEC-07, SEC-08)**:
   Path lokal, email pribadi, dan unit test yang membaca Credential Manager riil membocorkan data privat developer ke repository publik.

---

## 4. (b) Daftar Hal yang Sudah Bagus

1. **Pemisahan Sandboxing Akun yang Efektif**: Trik isolasi `SSH_CONNECTION=1` dan pengalihan `USERPROFILE`/`HOME` terbukti berhasil memisahkan kredensial worker profile dari Windows Credential Manager utama.
2. **Verifikasi Mandiri Kemampuan CLI**: Berhasil memverifikasi secara empiris bahwa `agy -p "/usage" --output-format json` dapat dijalankan dengan aman (0 token, 0 turn).
3. **UI/UX Visual yang Kaya & Modern**: Desain antarmuka WPF yang ekspansif dengan multi-row layout, chart headroom +35%, interactive tooltips, accordion drawer, dan visual themes yang menarik.
4. **Penanganan File Sharing yang Defensif**: Penggunaan `FileShare.ReadWrite` pada pembacaan file log dan token mencegah konflik file locking dengan proses CLI yang sedang aktif menulis.
5. **Dependency yang Ramping**: Menjaga dependency eksternal tetap minimal (hanya `CommunityToolkit.Mvvm`), meminimalisir attack surface supply chain.

---

## 5. (c) Rekomendasi Urutan Perbaikan (Fase 2)

### Gelombang 1: Critical Security & Integrity (Prioritas Tertinggi)
1. **[SEC-01, SEC-02, SEC-03]**: Perbaiki sanitasi dan escaping pada `TerminalLauncherService` (batch script generator, PowerShell runner, dan Windows Terminal args).
2. **[SEC-04]**: Terapkan validasi whitelist karakter nama profil dan pencegahan path traversal (`..` and canonical path checks).
3. **[SEC-05]**: Implementasikan enkripsi DPAPI (`ProtectedData`) untuk penyimpanan token/credential at-rest dengan fallback migrasi yang aman.
4. **[SEC-06, SEC-07, SEC-08]**: Bersihkan semua hardcoded email developer, path mesin lokal (`C:\Users\rifky`, `D:\Works`), dan buat unit test 100% hermetic tanpa membaca Credential Manager asli.

### Gelombang 2: Telemetri Dinamis & Konfigurasi (Sesuai Permintaan User)
5. **[TEL-01]**: Pindahkan tabel kuota tier dari hardcoded C# ke file konfigurasi `quota_config.json` yang dapat diedit oleh user.
6. **[TEL-02]**: Integrasikan parser JSON dari `agy -p "/usage" --output-format json` untuk telemetri autentik (weekly & 5-hour limit %, reset time UTC).

### Gelombang 3: Robustness, Error Handling & Performa
7. **[ERR-01]**: Ganti `async void` timer tick dan menu click dengan async task wrapper yang aman dan ber-logging.
8. **[ERR-02]**: Ganti seluruh blok `catch { }` kosong dengan logging terstruktur (`Logger.Warn`).
9. **[PERF-01, PERF-02]**: Optimalkan pembacaan file history (hindari `ReadAllLines` penuh) dan perbaiki buffer ring log di `Logger`.
10. **[UI-01]**: Perbaiki kecepatan scroll di tab Accounts dengan pengaturan mouse wheel delta / pixel-based scroll unit.

### Gelombang 4: Dokumentasi, CI/CD & Kebijakan Rilis
11. **[REL-01]**: Koreksi README.md: tegaskan bahwa aplikasi saat ini adalah **Windows Desktop GUI**, tandai Linux/macOS sebagai experimental/future Avalonia milestone, dan bersihkan script release workflow.
12. **[CI-01, CI-02]**: Tambahkan `dotnet test` ke `build.yml`, pin GitHub Actions ke immutable commit SHA, dan perketat workflow permissions.
13. **[DOC-01]**: Lengkapi `SECURITY.md` dengan Threat Model singkat dan tambahkan disclaimer ToS multi-akun di `README.md`.
