# Agy CLI Account Swarm v0.9.13-beta

Multiplatform Desktop Orchestrator & Sandbox Session Manager for Google Antigravity CLI (`agy`).

---

### 📦 Download Release Packages (Windows, Linux, macOS | x64 & ARM64)

| Platform | Architecture | File | Type |
| :--- | :--- | :--- | :--- |
| **Windows** | **x64** | `Agy-CLI-Account-Swarm-Setup-v0.9.13-beta.exe` | Inno Setup GUI Installer (Desktop shortcut & uninstaller) |
| **Windows** | **x64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-win-x64.zip` | Standalone Portable Archive |
| **Windows** | **ARM64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-win-arm64.zip` | Native ARM64 Portable (Snapdragon X Elite / Copilot+) |
| **Linux** | **x64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-linux-x64.tar.gz` | Tarball + `install.sh` (Wayland/X11, .desktop launcher) |
| **Linux** | **ARM64** | `Agy-CLI-Account-Swarm-v0.9.13-beta-linux-arm64.tar.gz` | Tarball + `install.sh` (aarch64 / Raspberry Pi / ARM VMs) |
| **macOS** | **Apple Silicon** | `Agy-CLI-Account-Swarm-v0.9.13-beta-macos-arm64.tar.gz` | `Agy CLI Account Swarm.app` + `install.sh` (M1/M2/M3/M4) |
| **macOS** | **Intel** | `Agy-CLI-Account-Swarm-v0.9.13-beta-macos-x64.tar.gz` | `Agy CLI Account Swarm.app` + `install.sh` (Intel x86_64) |
| **Checksums** | — | `SHA256SUMS.txt` | Official SHA256 integrity hash verification |

📖 **Panduan Instalasi Lengkap (Full Installation Guide)**: Lihat [`docs/INSTALLATION.md`](https://github.com/RifkyA911/agy-cli-account-swarm/blob/master/docs/INSTALLATION.md).

---

### 🌟 Ringkasan Pembaruan (Key Updates in v0.9.13-beta)

1. **Rilis Installer Lengkap Multiplatform**:
   - Paket installer Windows Inno Setup modern dengan uninstaller terintegrasi dan opsi portable ZIP untuk x64 dan ARM64.
   - Dukungan resmi Linux (x64 dan ARM64) dilengkapi skrip `install.sh` otomatis yang mendaftarkan ikon desktop dan perintah wrapper terminal `agy-cli-account-swarm`.
   - Dukungan resmi macOS (Apple Silicon M1/M2/M3/M4 dan Intel x64) dalam struktur bundle aplikasi `.app` dengan bypass otomatis karantina Gatekeeper (`xattr -cr`).

2. **Personal Chat Studio Modern (`/chat`)**:
   - Header 1 baris yang ringkas: Selector akun sandbox, tier chip, status live CLI, dynamic model selector, reasoning effort (`low`, `medium`, `high`), export Markdown, dan hapus sesi.
   - Pemuatan pesan instan & windowed pagination ("Biar Ringan") tanpa freeze UI thread.
   - Penambahan lampiran dokumen proyek RAG langsung di toolbar composer (`Attach Doc`).
   - Sinkronisasi riwayat CLI autentik dari `history.jsonl` dengan kontinuitas `--conversation <id>`.

3. **Swarm Workers Re-Architecture (`/dispatcher`)**:
   - Integrasi chat multi-agent bus langsung ke tab utama Swarm Workers bersama live telemetry stream, blackboard specs, dan dispatching worker.

4. **Rigorous Quality Standard**:
   - 170/170 hermetic unit test passed (100% green).
   - 0 build warnings, 0 build errors.
