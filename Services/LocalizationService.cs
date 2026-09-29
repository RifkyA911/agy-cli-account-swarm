using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgyAccountSwarm.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    string CurrentLanguage { get; set; }
    void SetLanguage(string langCode);
    string this[string key] { get; }
    string Get(string key);
}

public partial class LocalizationService : ObservableObject, ILocalizationService
{
    [ObservableProperty]
    private string _currentLanguage = "en";

    private readonly Dictionary<string, string> _en = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation
        ["Nav_Dashboard"] = "Dashboard",
        ["Nav_Accounts"] = "Accounts",
        ["Nav_Analytics"] = "Analytics",
        ["Nav_Mcp"] = "MCP Tools",
        ["Nav_Docs"] = "Documentation",
        ["Nav_Logs"] = "Logs",
        ["Nav_Settings"] = "Settings",
        ["Nav_About"] = "About",

        // Topbar
        ["App_Title"] = "Agy CLI Account Swarm",
        ["App_Subtitle"] = "Isolated multi-session manager, rate-limit monitor & quota tracker",
        ["App_VersionBadge"] = "v0.9.6-beta",
        ["Btn_SyncSwarm"] = "Sync Swarm",
        ["Btn_LaunchSwarm"] = "Launch Swarm",
        ["Tag_SyncedJustNow"] = "Synced just now",
        ["Alert_QuotaExhausted"] = "Quota Exhausted Alert",
        ["Btn_Dismiss"] = "Dismiss",

        // Sidebar
        ["Label_Sound"] = "Sound",
        ["Label_Theme"] = "Theme",
        ["Label_CliReady"] = "CLI Ready",
        ["Label_CliMissing"] = "CLI Missing",

        // Dashboard 5 Metric Cards
        ["Card_TotalAccounts"] = "TOTAL PROFILES",
        ["Card_TotalAccounts_Sub"] = "Sandboxed accounts",
        ["Card_Authenticated"] = "AUTHENTICATED",
        ["Card_Authenticated_Sub"] = "Active Google credentials",
        ["Card_SwarmTargets"] = "SWARM WORKERS",
        ["Card_SwarmTargets_Sub"] = "Selected for execution",
        ["Card_ActiveSessions"] = "SAVED CONVERSATIONS",
        ["Card_ActiveSessions_Sub"] = "Historical chat sessions",
        ["Card_McpTools"] = "MCP TOOLS & SERVERS",
        ["Card_McpTools_Sub"] = "Protocol integrations",
        ["Card_TotalPrompts"] = "SAVED CONVERSATIONS",
        ["Card_TotalPrompts_Sub"] = "Historical chat sessions",
        ["Card_EstTokens"] = "MCP TOOLS & SERVERS",
        ["Card_EstTokens_Sub"] = "Protocol integrations",

        // Chart
        ["Chart_Title"] = "Prompt & Token Activity Distribution",
        ["Chart_Subtitle"] = "Real-time telemetry parsed from local history.jsonl and session logs",
        ["Chart_Filter_Period"] = "Period",
        ["Chart_Filter_Model"] = "Model",
        ["Chart_Filter_Tier"] = "Tier",
        ["Chart_Mode_Bar"] = "Bar",
        ["Chart_Mode_Line"] = "Line",
        ["Chart_Mode_Area"] = "Area",
        ["Chart_NoData"] = "No recorded prompt activity found in session history yet.",

        // Accounts Actions & Labels
        ["Accounts_SearchPlaceholder"] = "Filter accounts by name, notes, email, model, or tier...",
        ["Accounts_SelectAll"] = "Select / Deselect All Swarm",
        ["Accounts_NewProfile"] = "New Profile",
        ["Accounts_Duplicate"] = "Duplicate",
        ["Accounts_EditProfile"] = "Edit Profile",
        ["Accounts_Delete"] = "Delete",
        ["Accounts_LaunchAgy"] = "▶ Launch agy",
        ["Accounts_StartConversation"] = "Start Conversation",
        ["Accounts_CopyCli"] = "Copy CLI",
        ["Accounts_Folder"] = "Open Folder",
        ["Accounts_PendingLogin"] = "Pending Login",
        ["Accounts_NotConnected"] = "Not Connected (Login Required)",
        ["Accounts_Velocity"] = "Velocity",
        ["Accounts_Headroom"] = "Headroom",
        ["Accounts_Role_Default"] = "Primary (Host Default)",
        ["Accounts_Role_Worker"] = "Isolated Sandbox Worker",

        // Profile Doctor
        ["Doctor_Title"] = "PROFILE DOCTOR",
        ["Doctor_Subtitle"] = "Audit AGY CLI Internal",
        ["Doctor_HealthCheck"] = "Health Check",
        ["Doctor_Checking"] = "Checking...",
        ["Doctor_CleanLocks"] = "Clean Stuck Locks",
        ["Doctor_TrustWorkspace"] = "Trust Workspace",
        ["Doctor_Clean"] = "Clean",
        ["Doctor_Warning"] = "Attention Required",

        // Analytics
        ["Analytics_Title"] = "Swarm & Model Intelligence Telemetry",
        ["Analytics_Subtitle"] = "Deep breakdown of real token utilization, model efficiency, and quota health",
        ["Analytics_Recalculate"] = "Re-Calculate Telemetry",
        ["Analytics_ModelMatrix"] = "AI Model Performance & Efficiency Matrix",
        ["Analytics_HeatmapTitle"] = "24-Hour Swarm Execution Heatmap",
        ["Analytics_AccountHealth"] = "Swarm Fleet Intelligence & CLI Capabilities",

        // MCP
        ["Mcp_Title"] = "Model Context Protocol (MCP) Integrations",
        ["Mcp_Subtitle"] = "Manage external tool providers and context sidecars configured for Antigravity AI",
        ["Mcp_AddServer"] = "+ Add MCP Server",
        ["Mcp_Reload"] = "Reload MCPs",
        ["Mcp_ActiveServers"] = "Active MCP Servers",
        ["Mcp_ToolsCount"] = "Tools Discovered",

        // Docs
        ["Docs_Title"] = "Architecture & Technical Documentation",
        ["Docs_Subtitle"] = "Comprehensive reference for isolation, process workflows, and configuration options",

        // Settings
        ["Settings_Title"] = "Application Settings & Preferences",
        ["Settings_Subtitle"] = "Configure terminal execution paths, swarm orchestration defaults, and system integration.",
        ["Settings_Language"] = "Display Language",
        ["Settings_TerminalGroup"] = "Terminal Launcher & Window Layout",
        ["Settings_TrayGroup"] = "Background & System Tray Integration",
        ["Settings_AudioGroup"] = "Audio Feedback & Sound Synthesizer",
        ["Settings_WelcomeGroup"] = "Cat Welcome Startup Animation",
        ["Settings_AboutGroup"] = "About & Open Source Credits",
        ["Settings_StorageGroup"] = "Storage, Profiles & Data Maintenance",

        // Dialogs
        ["Dialog_AddTitle"] = "Add Account Profile",
        ["Dialog_EditTitle"] = "Edit Profile: {0}",
        ["Dialog_DefaultNotice"] = "System Default Account: Sandbox path is locked to primary host environment (%USERPROFILE%\\.gemini)."
    };

    private readonly Dictionary<string, string> _id = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation
        ["Nav_Dashboard"] = "Dasbor",
        ["Nav_Accounts"] = "Akun Swarm",
        ["Nav_Analytics"] = "Analitik",
        ["Nav_Mcp"] = "Alat MCP",
        ["Nav_Docs"] = "Dokumentasi",
        ["Nav_Logs"] = "Catatan Log",
        ["Nav_Settings"] = "Pengaturan",
        ["Nav_About"] = "Tentang",

        // Topbar
        ["App_Title"] = "Agy CLI Account Swarm",
        ["App_Subtitle"] = "Manajer multi-sesi terisolasi, pemantau rate-limit & pelacak kuota",
        ["App_VersionBadge"] = "v0.9.6-beta",
        ["Btn_SyncSwarm"] = "Sinkron Swarm",
        ["Btn_LaunchSwarm"] = "Jalankan Swarm",
        ["Tag_SyncedJustNow"] = "Baru saja disinkronkan",
        ["Alert_QuotaExhausted"] = "Peringatan Kuota Habis",
        ["Btn_Dismiss"] = "Tutup",

        // Sidebar
        ["Label_Sound"] = "Suara",
        ["Label_Theme"] = "Tema",
        ["Label_CliReady"] = "CLI Siap",
        ["Label_CliMissing"] = "CLI Hilang",

        // Dashboard 5 Metric Cards
        ["Card_TotalAccounts"] = "TOTAL PROFIL",
        ["Card_TotalAccounts_Sub"] = "Akun terisolasi",
        ["Card_Authenticated"] = "TERAUTENTIKASI",
        ["Card_Authenticated_Sub"] = "Kredensial Google aktif",
        ["Card_SwarmTargets"] = "PEKERJA SWARM",
        ["Card_SwarmTargets_Sub"] = "Dipilih untuk eksekusi",
        ["Card_ActiveSessions"] = "SESI PERCAKAPAN",
        ["Card_ActiveSessions_Sub"] = "Riwayat chat tersimpan",
        ["Card_McpTools"] = "ALAT & SERVER MCP",
        ["Card_McpTools_Sub"] = "Integrasi protokol",
        ["Card_TotalPrompts"] = "SESI PERCAKAPAN",
        ["Card_TotalPrompts_Sub"] = "Riwayat chat tersimpan",
        ["Card_EstTokens"] = "ALAT & SERVER MCP",
        ["Card_EstTokens_Sub"] = "Integrasi protokol",

        // Chart
        ["Chart_Title"] = "Distribusi Aktivitas Prompt & Token",
        ["Chart_Subtitle"] = "Telemetri real-time diambil dari berkas riwayat history.jsonl & log",
        ["Chart_Filter_Period"] = "Periode",
        ["Chart_Filter_Model"] = "Model",
        ["Chart_Filter_Tier"] = "Tier",
        ["Chart_Mode_Bar"] = "Batang",
        ["Chart_Mode_Line"] = "Garis",
        ["Chart_Mode_Area"] = "Area",
        ["Chart_NoData"] = "Belum ada riwayat aktivitas prompt yang tercatat di sesi ini.",

        // Accounts Actions & Labels
        ["Accounts_SearchPlaceholder"] = "Cari profil berdasarkan nama, catatan, email, model, atau tier...",
        ["Accounts_SelectAll"] = "Pilih / Batalkan Semua Swarm",
        ["Accounts_NewProfile"] = "Tambah Profil",
        ["Accounts_Duplicate"] = "Duplikat",
        ["Accounts_EditProfile"] = "Edit Profil",
        ["Accounts_Delete"] = "Hapus",
        ["Accounts_LaunchAgy"] = "▶ Buka agy",
        ["Accounts_StartConversation"] = "Mulai Percakapan",
        ["Accounts_CopyCli"] = "Salin CLI",
        ["Accounts_Folder"] = "Buka Folder",
        ["Accounts_PendingLogin"] = "Belum Login",
        ["Accounts_NotConnected"] = "Belum Terhubung (Perlu Login)",
        ["Accounts_Velocity"] = "Kecepatan",
        ["Accounts_Headroom"] = "Kapasitas Sisa",
        ["Accounts_Role_Default"] = "Utama (Host Default)",
        ["Accounts_Role_Worker"] = "Worker Sandbox Terisolasi",

        // Profile Doctor
        ["Doctor_Title"] = "DOKTER PROFIL",
        ["Doctor_Subtitle"] = "Audit Jeroan AGY CLI",
        ["Doctor_HealthCheck"] = "Periksa Kesehatan",
        ["Doctor_Checking"] = "Memeriksa...",
        ["Doctor_CleanLocks"] = "Bersihkan Lock File",
        ["Doctor_TrustWorkspace"] = "Daftarkan Workspace",
        ["Doctor_Clean"] = "Bersih",
        ["Doctor_Warning"] = "Perlu Tindakan",

        // Analytics
        ["Analytics_Title"] = "Telemetri Intelijen Swarm & Model",
        ["Analytics_Subtitle"] = "Analisis mendalam konsumsi token riil, efisiensi model, dan kesehatan kuota",
        ["Analytics_Recalculate"] = "Hitung Ulang Telemetri",
        ["Analytics_ModelMatrix"] = "Matriks Kinerja & Efisiensi Model AI",
        ["Analytics_HeatmapTitle"] = "Peta Intensitas Eksekusi Swarm 24-Jam",
        ["Analytics_AccountHealth"] = "Armada Model & Kapabilitas agy CLI",

        // MCP
        ["Mcp_Title"] = "Integrasi Model Context Protocol (MCP)",
        ["Mcp_Subtitle"] = "Kelola penyedia tools eksternal dan sidecar context untuk Antigravity AI",
        ["Mcp_AddServer"] = "+ Tambah Server MCP",
        ["Mcp_Reload"] = "Muat Ulang MCP",
        ["Mcp_ActiveServers"] = "Server MCP Aktif",
        ["Mcp_ToolsCount"] = "Alat Ditemukan",

        // Docs
        ["Docs_Title"] = "Arsitektur & Dokumentasi Teknis",
        ["Docs_Subtitle"] = "Panduan lengkap isolasi direktori, alur kerja proses, dan konfigurasi",

        // Settings
        ["Settings_Title"] = "Pengaturan & Preferensi Aplikasi",
        ["Settings_Subtitle"] = "Atur launcher terminal, default orkestrator swarm, bahasa, dan integrasi sistem tray.",
        ["Settings_Language"] = "Bahasa Tampilan",
        ["Settings_TerminalGroup"] = "Launcher Terminal & Tata Letak Jendela",
        ["Settings_TrayGroup"] = "Integrasi Background & System Tray",
        ["Settings_AudioGroup"] = "Umpan Balik Audio & Efek Suara",
        ["Settings_WelcomeGroup"] = "Animasi Sapaan Kucing",
        ["Settings_AboutGroup"] = "Tentang & Kontributor Open Source",
        ["Settings_StorageGroup"] = "Penyimpanan, Profil & Pemeliharaan Data",

        // Dialogs
        ["Dialog_AddTitle"] = "Tambah Profil Akun",
        ["Dialog_EditTitle"] = "Edit Profil: {0}",
        ["Dialog_DefaultNotice"] = "Akun Default Sistem: Path sandbox terkunci ke lingkungan host utama (%USERPROFILE%\\.gemini)."
    };

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        var dict = CurrentLanguage.Equals("id", StringComparison.OrdinalIgnoreCase) ? _id : _en;
        if (dict.TryGetValue(key, out var val)) return val;
        if (_en.TryGetValue(key, out var fallback)) return fallback;
        return key;
    }

    public void SetLanguage(string langCode)
    {
        CurrentLanguage = langCode.Equals("id", StringComparison.OrdinalIgnoreCase) ? "id" : "en";
        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged("Item[]");
    }
}
