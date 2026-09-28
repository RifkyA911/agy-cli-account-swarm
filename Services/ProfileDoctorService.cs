using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public enum DoctorCheckStatus
{
    Passed,
    Warning,
    Failed
}

public class DoctorCheckItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DoctorCheckStatus Status { get; set; } = DoctorCheckStatus.Passed;
    public string Message { get; set; } = string.Empty;
    public string? ActionHint { get; set; }

    public string StatusIcon => Status switch
    {
        DoctorCheckStatus.Passed => "✅",
        DoctorCheckStatus.Warning => "⚠️",
        DoctorCheckStatus.Failed => "❌",
        _ => "ℹ️"
    };

    public string StatusBadgeColor => Status switch
    {
        DoctorCheckStatus.Passed => "#10B981",
        DoctorCheckStatus.Warning => "#F59E0B",
        DoctorCheckStatus.Failed => "#EF4444",
        _ => "#6B7280"
    };
}

public class ProfileDoctorReport
{
    public AccountProfile Profile { get; set; } = null!;
    public DateTime RunAt { get; set; } = DateTime.UtcNow;
    public DoctorCheckStatus OverallStatus { get; set; } = DoctorCheckStatus.Passed;
    public List<DoctorCheckItem> Checks { get; set; } = new();
    public int LockFilesCount { get; set; }
    public List<string> LockFiles { get; set; } = new();

    public string OverallStatusText => OverallStatus switch
    {
        DoctorCheckStatus.Passed => "Healthy • Semua Sistem Normal",
        DoctorCheckStatus.Warning => "Perhatian • Butuh Tindakan Ringan",
        DoctorCheckStatus.Failed => "Error • Konfigurasi Perlu Diperbaiki",
        _ => "Belum Diperiksa"
    };

    public string OverallStatusColor => OverallStatus switch
    {
        DoctorCheckStatus.Passed => "#10B981",
        DoctorCheckStatus.Warning => "#F59E0B",
        DoctorCheckStatus.Failed => "#EF4444",
        _ => "#6B7280"
    };
}

public interface IProfileDoctorService
{
    Task<ProfileDoctorReport> DiagnoseProfileAsync(AccountProfile profile);
    Task<int> CleanStuckLocksAsync(AccountProfile profile);
    Task<bool> AddWorkspaceToTrustedAsync(AccountProfile profile);
}

public class ProfileDoctorService : IProfileDoctorService
{
    private readonly DataProtectionService _dataProtectionService = new();

    public async Task<ProfileDoctorReport> DiagnoseProfileAsync(AccountProfile profile)
    {
        return await Task.Run(async () =>
        {
            var report = new ProfileDoctorReport
            {
                Profile = profile,
                RunAt = DateTime.UtcNow
            };

            // 1. Check AGY CLI Installation & Version
            var cliCheck = await CheckAgyCliVersionAsync();
            report.Checks.Add(cliCheck);

            // 2. Check OAuth Token Validity & Expiry
            var tokenCheck = CheckOAuthTokenExpiry(profile);
            report.Checks.Add(tokenCheck);

            // 3. Check Lingering Lock Files
            var lockCheck = CheckLingeringLockFiles(profile, report);
            report.Checks.Add(lockCheck);

            // 4. Check Workspace Trust Registration
            var workspaceCheck = CheckWorkspaceTrust(profile);
            report.Checks.Add(workspaceCheck);

            // 5. Check /usage CLI Schema Compatibility
            var usageCheck = await CheckUsageSchemaCompatibilityAsync(profile);
            report.Checks.Add(usageCheck);

            // Calculate Overall Status
            if (report.Checks.Any(c => c.Status == DoctorCheckStatus.Failed))
            {
                report.OverallStatus = DoctorCheckStatus.Failed;
            }
            else if (report.Checks.Any(c => c.Status == DoctorCheckStatus.Warning))
            {
                report.OverallStatus = DoctorCheckStatus.Warning;
            }
            else
            {
                report.OverallStatus = DoctorCheckStatus.Passed;
            }

            return report;
        });
    }

    private static async Task<DoctorCheckItem> CheckAgyCliVersionAsync()
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c agy --version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            proc.Start();
            var readTask = proc.StandardOutput.ReadToEndAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(4000));

            if (completed == readTask && proc.WaitForExit(1000) && proc.ExitCode == 0)
            {
                var version = (await readTask).Trim();
                if (!string.IsNullOrWhiteSpace(version))
                {
                    return new DoctorCheckItem
                    {
                        Key = "cli_version",
                        Title = "Versi agy Terpasang",
                        Status = DoctorCheckStatus.Passed,
                        Message = $"CLI terdeteksi: {version}"
                    };
                }
            }

            return new DoctorCheckItem
            {
                Key = "cli_version",
                Title = "Versi agy Terpasang",
                Status = DoctorCheckStatus.Failed,
                Message = "Perintah 'agy' tidak merespons atau tidak ditemukan di sistem PATH",
                ActionHint = "Pasang agy CLI atau pastikan path bin sudah terdaftar di environment PATH"
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheckItem
            {
                Key = "cli_version",
                Title = "Versi agy Terpasang",
                Status = DoctorCheckStatus.Failed,
                Message = $"Gagal memverifikasi agy CLI: {ex.Message}",
                ActionHint = "Periksa instalasi agy CLI di sistem"
            };
        }
    }

    private DoctorCheckItem CheckOAuthTokenExpiry(AccountProfile profile)
    {
        var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
        var tokenPath = Path.Combine(profileDir, ".gemini", "antigravity-cli", "antigravity-oauth-token");

        if (!File.Exists(tokenPath))
        {
            if (profile.IsMainDefaultProfile())
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var globalToken = Path.Combine(userProfile, ".gemini", "antigravity-cli", "antigravity-oauth-token");
                if (File.Exists(globalToken))
                {
                    tokenPath = globalToken;
                }
            }
        }

        if (!File.Exists(tokenPath))
        {
            return new DoctorCheckItem
            {
                Key = "oauth_token",
                Title = "Token Otentikasi OAuth",
                Status = DoctorCheckStatus.Failed,
                Message = "File token OAuth belum dibuat (akun belum login)",
                ActionHint = "Buka sesi terminal dengan tombol 'Launch agy' untuk menyelesaikan login Google"
            };
        }

        try
        {
            var raw = File.ReadAllText(tokenPath);
            var tokenJson = _dataProtectionService.Unprotect(raw);

            if (string.IsNullOrWhiteSpace(tokenJson))
            {
                return new DoctorCheckItem
                {
                    Key = "oauth_token",
                    Title = "Token Otentikasi OAuth",
                    Status = DoctorCheckStatus.Failed,
                    Message = "File token kosong atau tidak dapat didekripsi",
                    ActionHint = "Login ulang diperlukan"
                };
            }

            // Check expiry inside JSON
            using var doc = JsonDocument.Parse(tokenJson);
            long expSeconds = 0;

            if (doc.RootElement.TryGetProperty("id_token", out var idTokenProp) &&
                idTokenProp.GetString() is { Length: > 0 } idToken)
            {
                var parts = idToken.Split('.');
                if (parts.Length >= 2)
                {
                    string payload = parts[1].Replace('-', '+').Replace('_', '/');
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }
                    var bytes = Convert.FromBase64String(payload);
                    using var jwtDoc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
                    if (jwtDoc.RootElement.TryGetProperty("exp", out var expProp) && expProp.TryGetInt64(out var expVal))
                    {
                        expSeconds = expVal;
                    }
                }
            }

            if (expSeconds == 0 && doc.RootElement.TryGetProperty("exp", out var rootExp) && rootExp.TryGetInt64(out var rVal))
            {
                expSeconds = rVal;
            }

            if (expSeconds > 0)
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime;
                if (DateTime.UtcNow >= expDate)
                {
                    return new DoctorCheckItem
                    {
                        Key = "oauth_token",
                        Title = "Token Otentikasi OAuth",
                        Status = DoctorCheckStatus.Warning,
                        Message = $"Token telah kadaluarsa pada {expDate:dd MMM yyyy HH:mm} UTC (refresh token otomatis diperlukan saat launch)",
                        ActionHint = "Jalankan agy untuk melakukan refresh token otomatis"
                    };
                }

                var remaining = expDate - DateTime.UtcNow;
                var remainingStr = remaining.TotalDays >= 1
                    ? $"{remaining.Days} hari {remaining.Hours} jam"
                    : $"{remaining.Hours} jam {remaining.Minutes} menit";

                return new DoctorCheckItem
                {
                    Key = "oauth_token",
                    Title = "Token Otentikasi OAuth",
                    Status = DoctorCheckStatus.Passed,
                    Message = $"Token valid dan aktif (Sisa masa aktif: {remainingStr})"
                };
            }

            // If no explicit exp claim, check file age
            var lastWrite = File.GetLastWriteTime(tokenPath);
            return new DoctorCheckItem
            {
                Key = "oauth_token",
                Title = "Token Otentikasi OAuth",
                Status = DoctorCheckStatus.Passed,
                Message = $"Token aktif terverifikasi (Terakhir diperbarui {lastWrite:dd MMM yyyy HH:mm})"
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheckItem
            {
                Key = "oauth_token",
                Title = "Token Otentikasi OAuth",
                Status = DoctorCheckStatus.Failed,
                Message = $"Gagal memvalidasi token: {ex.Message}",
                ActionHint = "Periksa izin baca file atau lakukan login ulang"
            };
        }
    }

    private static DoctorCheckItem CheckLingeringLockFiles(AccountProfile profile, ProfileDoctorReport report)
    {
        var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
        var cliDir = Path.Combine(profileDir, ".gemini", "antigravity-cli");

        var locks = new List<string>();

        if (Directory.Exists(cliDir))
        {
            try
            {
                // Find all *.lock files in the CLI directory and subdirectories
                var files = Directory.GetFiles(cliDir, "*.lock", SearchOption.AllDirectories);
                locks.AddRange(files);
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ProfileDoctor] Error scanning locks: {ex.Message}");
            }
        }

        report.LockFiles = locks;
        report.LockFilesCount = locks.Count;

        if (locks.Count > 0)
        {
            var lockNames = string.Join(", ", locks.Select(Path.GetFileName));
            return new DoctorCheckItem
            {
                Key = "lock_files",
                Title = "Lock File Nyangkut",
                Status = DoctorCheckStatus.Warning,
                Message = $"Terdeteksi {locks.Count} file lock nyangkut: {lockNames}",
                ActionHint = "Klik 'Bersihkan Lock File' untuk menghapus file lock yang tertinggal dari sesi sebelumnya"
            };
        }

        return new DoctorCheckItem
        {
            Key = "lock_files",
            Title = "Lock File Nyangkut",
            Status = DoctorCheckStatus.Passed,
            Message = "Bersih • Tidak ada file lock nyangkut terdeteksi"
        };
    }

    private static DoctorCheckItem CheckWorkspaceTrust(AccountProfile profile)
    {
        var ws = profile.DefaultWorkspace;
        if (string.IsNullOrWhiteSpace(ws))
        {
            return new DoctorCheckItem
            {
                Key = "workspace_trust",
                Title = "Status Trusted Workspace",
                Status = DoctorCheckStatus.Passed,
                Message = "Default Home Workspace (otomatis terpercaya)"
            };
        }

        if (!Directory.Exists(ws))
        {
            return new DoctorCheckItem
            {
                Key = "workspace_trust",
                Title = "Status Trusted Workspace",
                Status = DoctorCheckStatus.Warning,
                Message = $"Folder workspace tidak ditemukan di disk: {ws}",
                ActionHint = "Buat folder tersebut atau perbarui path pada Edit Profile"
            };
        }

        // Check settings.json in profile sandbox and in user's home .gemini
        var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
        var localSettingsPath = Path.Combine(profileDir, ".gemini", "antigravity-cli", "settings.json");
        var userHomeSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".gemini", "antigravity-cli", "settings.json");

        var trustedList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void ExtractTrusted(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("trustedWorkspaces", out var twProp) &&
                    twProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in twProp.EnumerateArray())
                    {
                        var str = item.GetString();
                        if (!string.IsNullOrWhiteSpace(str))
                        {
                            try
                            {
                                trustedList.Add(Path.GetFullPath(str).TrimEnd('\\', '/'));
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[ProfileDoctor] Error reading settings for trusted workspaces: {ex.Message}");
            }
        }

        ExtractTrusted(localSettingsPath);
        ExtractTrusted(userHomeSettingsPath);

        string normalizedWs;
        try
        {
            normalizedWs = Path.GetFullPath(ws).TrimEnd('\\', '/');
        }
        catch
        {
            normalizedWs = ws.TrimEnd('\\', '/');
        }

        if (trustedList.Contains(normalizedWs))
        {
            return new DoctorCheckItem
            {
                Key = "workspace_trust",
                Title = "Status Trusted Workspace",
                Status = DoctorCheckStatus.Passed,
                Message = "Folder workspace sudah terdaftar di trustedWorkspaces"
            };
        }

        return new DoctorCheckItem
        {
            Key = "workspace_trust",
            Title = "Status Trusted Workspace",
            Status = DoctorCheckStatus.Warning,
            Message = "Workspace belum masuk daftar trustedWorkspaces (dapat memicu konfirmasi manual)",
            ActionHint = "Klik 'Daftarkan Workspace' untuk mendaftarkan workspace secara otomatis"
        };
    }

    private static async Task<DoctorCheckItem> CheckUsageSchemaCompatibilityAsync(AccountProfile profile)
    {
        try
        {
            var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c agy -p \"/usage\" --output-format json",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Isolate sandbox environment if not main profile
            if (!profile.IsMainDefaultProfile() && Directory.Exists(profileDir))
            {
                proc.StartInfo.Environment["USERPROFILE"] = profileDir;
                proc.StartInfo.Environment["HOME"] = profileDir;
            }

            proc.Start();
            var readTask = proc.StandardOutput.ReadToEndAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(6000));

            if (completed == readTask && proc.WaitForExit(1000) && proc.ExitCode == 0)
            {
                var json = await readTask;
                var parseResult = AgyUsageParser.Parse(json);
                if (parseResult.IsSuccess)
                {
                    return new DoctorCheckItem
                    {
                        Key = "usage_schema",
                        Title = "Format /usage agy CLI",
                        Status = DoctorCheckStatus.Passed,
                        Message = "Skema JSON cocok (Model quota groups & buckets terdeteksi)"
                    };
                }

                return new DoctorCheckItem
                {
                    Key = "usage_schema",
                    Title = "Format /usage agy CLI",
                    Status = DoctorCheckStatus.Warning,
                    Message = "Output didapat tetapi struktur skema JSON berbeda dari format standar",
                    ActionHint = "Periksa pembaruan versi agy CLI"
                };
            }

            return new DoctorCheckItem
            {
                Key = "usage_schema",
                Title = "Format /usage agy CLI",
                Status = DoctorCheckStatus.Warning,
                Message = "Perintah agy -p /usage tidak merespons dalam 6 detik atau memerlukan autentikasi",
                ActionHint = "Pastikan akun sudah login dan memiliki akses internet"
            };
        }
        catch (Exception ex)
        {
            return new DoctorCheckItem
            {
                Key = "usage_schema",
                Title = "Format /usage agy CLI",
                Status = DoctorCheckStatus.Warning,
                Message = $"Gagal memeriksa /usage: {ex.Message}"
            };
        }
    }

    public Task<int> CleanStuckLocksAsync(AccountProfile profile)
    {
        return Task.Run(() =>
        {
            int deleted = 0;
            var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
            var cliDir = Path.Combine(profileDir, ".gemini", "antigravity-cli");

            if (!Directory.Exists(cliDir)) return 0;

            try
            {
                var files = Directory.GetFiles(cliDir, "*.lock", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deleted++;
                        Logger.Info($"[ProfileDoctor] Deleted stuck lock file: {file}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[ProfileDoctor] Could not delete lock file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[ProfileDoctor] Error while cleaning locks", ex);
            }

            return deleted;
        });
    }

    public Task<bool> AddWorkspaceToTrustedAsync(AccountProfile profile)
    {
        return Task.Run(() =>
        {
            var ws = profile.DefaultWorkspace;
            if (string.IsNullOrWhiteSpace(ws) || !Directory.Exists(ws)) return false;

            var profileDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
            var settingsDir = Path.Combine(profileDir, ".gemini", "antigravity-cli");
            Directory.CreateDirectory(settingsDir);

            var settingsPath = Path.Combine(settingsDir, "settings.json");

            try
            {
                var normalizedWs = Path.GetFullPath(ws).TrimEnd('\\', '/');
                Dictionary<string, object?> dict;

                if (File.Exists(settingsPath))
                {
                    var text = File.ReadAllText(settingsPath);
                    dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(text) ?? new();
                }
                else
                {
                    dict = new();
                }

                var list = new List<string>();
                if (dict.TryGetValue("trustedWorkspaces", out var existingObj) && existingObj is JsonElement el && el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        if (item.GetString() is { Length: > 0 } s)
                        {
                            list.Add(s);
                        }
                    }
                }

                if (!list.Any(s => string.Equals(s, normalizedWs, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(normalizedWs);
                }

                dict["trustedWorkspaces"] = list;
                var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json, Encoding.UTF8);

                Logger.Info($"[ProfileDoctor] Added workspace '{normalizedWs}' to trustedWorkspaces in {settingsPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[ProfileDoctor] Failed to add workspace to trustedWorkspaces", ex);
                return false;
            }
        });
    }
}
