# 📦 Panduan Instalasi & Penggunaan (Installation Guide)
**Agy CLI Account Swarm — Multiplatform (Windows, Linux, macOS | x64 & ARM64)**

Dokumen ini menyediakan panduan lengkap instalasi, konfigurasi, dan verifikasi untuk seluruh platform yang didukung oleh **Agy CLI Account Swarm**.

---

## 🚀 Daftar Paket Rilis (Release Packages)

Setiap rilis resmi menyediakan paket biner yang telah dioptimasi untuk arsitektur masing-masing:

| Platform | Arsitektur | Format Paket | Deskripsi & File |
| :--- | :--- | :--- | :--- |
| **Windows** | **x64** (Intel/AMD) | Installer GUI (`.exe`) | `Agy-CLI-Account-Swarm-Setup-v<ver>.exe`<br>*(Wizard Inno Setup resmi dengan desktop icon & uninstaller)* |
| **Windows** | **x64** (Intel/AMD) | Portable (`.zip`) | `Agy-CLI-Account-Swarm-v<ver>-win-x64.zip`<br>*(Ekstrak dan langsung jalankan)* |
| **Windows** | **ARM64** (Copilot+ / Surface) | Portable (`.zip`) | `Agy-CLI-Account-Swarm-v<ver>-win-arm64.zip`<br>*(Native ARM64 untuk Snapdragon X Elite & Surface Pro)* |
| **Linux** | **x64** (x86_64) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-linux-x64.tar.gz`<br>*(Termasuk `install.sh`, launcher `.desktop`, & icon)* |
| **Linux** | **ARM64** (aarch64) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-linux-arm64.tar.gz`<br>*(Untuk Raspberry Pi 5, server ARM, & VM aarch64)* |
| **macOS** | **Apple Silicon** (M1/M2/M3/M4) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-macos-arm64.tar.gz`<br>*(Termasuk bundle `Agy CLI Account Swarm.app` & installer)* |
| **macOS** | **Intel** (x86_64) | Tarball (`.tar.gz`) | `Agy-CLI-Account-Swarm-v<ver>-macos-x64.tar.gz`<br>*(Untuk Mac berbasis prosesor Intel)* |
| **Semua** | — | Checksums (`.txt`) | `SHA256SUMS.txt`<br>*(Daftar hash integritas biner resmi)* |

---

## 🪟 1. Panduan Instalasi Windows (x64 & ARM64)

### Metode A: Installer Setup Wizard (Rekomendasi untuk x64)
1. Unduh **`Agy-CLI-Account-Swarm-Setup-v<ver>.exe`** dari halaman [GitHub Releases](https://github.com/RifkyA911/agy-cli-account-swarm/releases).
2. Jalankan file `.exe`. Jika muncul peringatan Windows SmartScreen (*Unknown Publisher*), klik **More info** ➔ **Run anyway**.
3. Ikuti wizard:
   - Pilih opsi pembuatan shortcut Desktop jika diinginkan.
   - Aplikasi akan terpasang di `%LOCALAPPDATA%\Programs\Agy CLI Account Swarm` tanpa memerlukan hak administrator (UAC).
4. Klik **Finish** untuk langsung membuka aplikasi.
5. Untuk menghapus (uninstall), gunakan menu **Settings ➔ Installed Apps** di Windows atau shortcut **Uninstall Agy CLI Account Swarm** di Start Menu.

### Metode B: Portable ZIP (Windows x64 / ARM64)
1. Unduh `Agy-CLI-Account-Swarm-v<ver>-win-x64.zip` atau `Agy-CLI-Account-Swarm-v<ver>-win-arm64.zip`.
2. Klik kanan ➔ **Extract All...** ke folder pilihan Anda (misalnya `D:\Tools\AgyAccountSwarm`).
3. Jalankan **`AgyCliAccountSwarmGUI.exe`**.

> [!TIP]
> Pastikan Google Antigravity CLI (`agy`) sudah terpasang di sistem Anda. Cek via PowerShell:
> ```powershell
> agy --version
> ```

---

## 🐧 2. Panduan Instalasi Linux (x64 & ARM64)

Mendukung semua distro Linux modern (Ubuntu/Debian, Fedora, Arch Linux, openSUSE) baik pada lingkungan **Wayland** maupun **X11**.

### Prasyarat:
Pastikan paket runtime dasar grafis terpasang:
- **Ubuntu/Debian**: `sudo apt install libx11-6 libice6 libsm6 libfontconfig1`
- **Fedora/RHEL**: `sudo dnf install libX11 libICE libSM fontconfig`
- **Arch Linux**: `sudo pacman -S libx11 libice libsm fontconfig`

### Langkah Instalasi:
1. Unduh paket tarball yang sesuai:
   - Untuk PC/Server Intel/AMD 64-bit: `Agy-CLI-Account-Swarm-v<ver>-linux-x64.tar.gz`
   - Untuk ARM64 (aarch64): `Agy-CLI-Account-Swarm-v<ver>-linux-arm64.tar.gz`
2. Ekstrak arsip melalui terminal:
   ```bash
   tar -xzf Agy-CLI-Account-Swarm-v*-linux-*.tar.gz
   cd agy-cli-account-swarm-v*-linux-*
   ```
3. Jalankan skrip installer:
   ```bash
   bash install.sh
   ```
   *Skrip otomatis:*
   - Menyalin biner ke `~/.local/share/agy-cli-account-swarm` (atau `/opt` jika dijalankan dengan `sudo`).
   - Membuat wrapper CLI di `~/.local/bin/agy-cli-account-swarm`.
   - Mendaftarkan entri menu desktop (`.desktop`) dan ikon aplikasi di app drawer Linux Anda.
4. Buka aplikasi dari menu aplikasi desktop Anda atau ketik perintah:
   ```bash
   agy-cli-account-swarm
   ```

### Menghapus Instalasi (Uninstall):
Cukup jalankan:
```bash
~/.local/share/agy-cli-account-swarm/uninstall.sh
```

---

## 🍎 3. Panduan Instalasi macOS (Apple Silicon & Intel)

Mendukung macOS Sonoma, Sequoia, dan versi sebelumnya pada Apple Silicon (M1, M2, M3, M4) serta Intel Mac.

### Langkah Instalasi:
1. Unduh paket tarball:
   - Untuk Mac Apple Silicon (M1/M2/M3/M4): `Agy-CLI-Account-Swarm-v<ver>-macos-arm64.tar.gz`
   - Untuk Mac berbasis Intel: `Agy-CLI-Account-Swarm-v<ver>-macos-x64.tar.gz`
2. Ekstrak arsip:
   ```bash
   tar -xzf Agy-CLI-Account-Swarm-v*-macos-*.tar.gz
   cd agy-cli-account-swarm-v*-macos-*
   ```
3. Jalankan skrip installer:
   ```bash
   bash install.sh
   ```
   *Skrip otomatis:*
   - Memasang `Agy CLI Account Swarm.app` ke direktori `/Applications` (atau `~/Applications`).
   - Membersihkan atribut karantina macOS Gatekeeper (`xattr -cr`).
   - Membuat symlink terminal di `~/.local/bin/agy-cli-account-swarm` (atau `/usr/local/bin`).
4. Buka aplikasi dari **Launchpad**, **Spotlight** (`Cmd + Space` ➔ `Agy CLI Account Swarm`), atau via terminal:
   ```bash
   agy-cli-account-swarm
   ```

> [!NOTE]
> Jika macOS menampilkan dialog *"Agy CLI Account Swarm cannot be opened because the developer cannot be verified"*:
> Buka **System Settings ➔ Privacy & Security**, gulir ke bawah ke bagian **Security**, lalu klik **Open Anyway**. Atau jalankan perintah berikut di terminal:
> ```bash
> xattr -cr "/Applications/Agy CLI Account Swarm.app"
> ```

### Menghapus Instalasi (Uninstall):
Jalankan skrip uninstaller:
```bash
"/Applications/Agy CLI Account Swarm.app/Contents/Resources/uninstall.sh"
```

---

## 🔐 4. Verifikasi Integritas SHA256

Untuk memastikan file installer yang diunduh tidak rusak atau dimodifikasi:

### Di Windows (PowerShell):
```powershell
Get-FileHash -Path .\Agy-CLI-Account-Swarm-Setup-v*.exe -Algorithm SHA256
```

### Di Linux / macOS:
```bash
sha256sum Agy-CLI-Account-Swarm-v*.tar.gz
```
Bandingkan nilai hash yang dihasilkan dengan file `SHA256SUMS.txt` resmi pada halaman rilis GitHub.

---

## ⚙️ 5. Prasyarat Sistem & Antigravity CLI (`agy`)

Agy CLI Account Swarm adalah orkestrator antarmuka grafis desktop untuk **Google Antigravity CLI**.

1. Pastikan Anda telah memasang **Google Antigravity CLI**:
   - Ikuti panduan resmi instalasi `agy`.
   - Pastikan biner `agy` dapat dipanggil dari terminal shell Anda.
2. Login akun Google utama Anda:
   ```bash
   agy login
   ```
3. Buka **Agy CLI Account Swarm**:
   - Akun utama Anda akan langsung terdeteksi otomatis beserta kuota live 5-jam & mingguan.
   - Klik **`+ Add Profile`** untuk membuat sandbox profil kedua, ketiga, dan seterusnya dengan direktori terisolasi (`~/.gemini-profiles/<id>`) dan kredensial terpisah tanpa saling bentrok.

---

## 💬 Dukungan & Masalah
Jika Anda menemui kendala teknis atau memiliki usulan fitur:
- Laporkan Issue: [GitHub Issues](https://github.com/RifkyA911/agy-cli-account-swarm/issues)
- Panduan Pemecahan Masalah: [`docs/TROUBLESHOOTING.md`](TROUBLESHOOTING.md)
