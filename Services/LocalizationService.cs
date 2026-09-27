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

        // Topbar
        ["App_Title"] = "Agy Account Swarm",
        ["App_Subtitle"] = "Isolated multi-session manager, rate-limit monitor & quota tracker",
        ["App_VersionBadge"] = "v0.9.0-beta (MIT)",
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

        // Dashboard
        ["Card_TotalAccounts"] = "TOTAL ACCOUNTS",
        ["Card_TotalAccounts_Sub"] = "Managed profiles",
        ["Card_Authenticated"] = "AUTHENTICATED",
        ["Card_Authenticated_Sub"] = "Active Google tokens",
        ["Card_SwarmTargets"] = "SWARM TARGETS",
        ["Card_SwarmTargets_Sub"] = "Queued for parallel launch",
        ["Card_TotalPrompts"] = "TOTAL PROMPTS",
        ["Card_TotalPrompts_Sub"] = "Real session turns",
        ["Card_EstTokens"] = "ESTIMATED TOKENS",
        ["Card_EstTokens_Sub"] = "Swarm consumption",

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

        // Accounts
        ["Accounts_SearchPlaceholder"] = "Filter accounts by name, notes, email, model, or tier...",
        ["Accounts_SelectAll"] = "Select / Deselect All Swarm",
        ["Accounts_NewProfile"] = "New Profile",
        ["Accounts_Duplicate"] = "Duplicate",
        ["Accounts_LaunchAgy"] = "▶ Launch agy",
        ["Accounts_CopyCli"] = "📋 Copy",
        ["Accounts_Folder"] = "📁 Folder",
        ["Accounts_Edit"] = "✏️",
        ["Accounts_Delete"] = "🗑️",
        ["Accounts_PendingLogin"] = "Pending Login",
        ["Accounts_NotConnected"] = "Not Connected (Login Required)",

        // Analytics
        ["Analytics_Title"] = "Swarm & Model Intelligence Telemetry",
        ["Analytics_Subtitle"] = "Deep breakdown of real token utilization, model efficiency, and quota health",
        ["Analytics_Recalculate"] = "Re-Calculate Telemetry",
        ["Analytics_ModelMatrix"] = "AI Model Performance & Efficiency Matrix",
        ["Analytics_HeatmapTitle"] = "24-Hour Swarm Execution Heatmap",
        ["Analytics_AccountHealth"] = "Account Swarm Health & Rate-Limit Safety",

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
        ["Settings_StorageGroup"] = "Storage, Profiles & Data Maintenance"
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

        // Topbar
        ["App_Title"] = "Agy Account Swarm",
        ["App_Subtitle"] = "Manajer multi-sesi terisolasi, pemantau rate-limit & pelacak kuota",
        ["App_VersionBadge"] = "v0.9.0-beta (MIT)",
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

        // Dashboard
        ["Card_TotalAccounts"] = "TOTAL AKUN",
        ["Card_TotalAccounts_Sub"] = "Profil terdaftar",
        ["Card_Authenticated"] = "TERAUTENTIKASI",
        ["Card_Authenticated_Sub"] = "Token Google aktif",
        ["Card_SwarmTargets"] = "TARGET SWARM",
        ["Card_SwarmTargets_Sub"] = "Siap diluncurkan paralel",
        ["Card_TotalPrompts"] = "TOTAL PROMPT",
        ["Card_TotalPrompts_Sub"] = "Turn sesi aktual",
        ["Card_EstTokens"] = "ESTIMASI TOKEN",
        ["Card_EstTokens_Sub"] = "Konsumsi swarm",

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

        // Accounts
        ["Accounts_SearchPlaceholder"] = "Cari profil berdasarkan nama, catatan, email, model, atau tier...",
        ["Accounts_SelectAll"] = "Pilih / Batalkan Semua Swarm",
        ["Accounts_NewProfile"] = "Tambah Profil",
        ["Accounts_Duplicate"] = "Duplikat",
        ["Accounts_LaunchAgy"] = "▶ Buka agy",
        ["Accounts_CopyCli"] = "📋 Salin",
        ["Accounts_Folder"] = "📁 Folder",
        ["Accounts_Edit"] = "✏️",
        ["Accounts_Delete"] = "🗑️",
        ["Accounts_PendingLogin"] = "Belum Login",
        ["Accounts_NotConnected"] = "Belum Terhubung (Perlu Login)",

        // Analytics
        ["Analytics_Title"] = "Telemetri Intelijen Swarm & Model",
        ["Analytics_Subtitle"] = "Analisis mendalam konsumsi token riil, efisiensi model, dan kesehatan kuota",
        ["Analytics_Recalculate"] = "Hitung Ulang Telemetri",
        ["Analytics_ModelMatrix"] = "Matriks Kinerja & Efisiensi Model AI",
        ["Analytics_HeatmapTitle"] = "Peta Intensitas Eksekusi Swarm 24-Jam",
        ["Analytics_AccountHealth"] = "Kesehatan Akun Swarm & Keamanan Kuota",

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
        ["Settings_StorageGroup"] = "Penyimpanan, Profil & Pemeliharaan Data"
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
