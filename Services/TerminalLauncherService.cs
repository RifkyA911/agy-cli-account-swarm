using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class TerminalLauncherService : ITerminalLauncherService
{
    private string? _cachedAgyPath;

    public string? FindAgyExecutablePath()
    {
        if (_cachedAgyPath != null && File.Exists(_cachedAgyPath))
        {
            return _cachedAgyPath;
        }

        // 1. Common install location for Antigravity CLI on Windows
        var userLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var standardPath = Path.Combine(userLocal, "agy", "bin", "agy.exe");
        if (File.Exists(standardPath))
        {
            _cachedAgyPath = standardPath;
            return standardPath;
        }

        // 2. Check in PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            var candidate = Path.Combine(p.Trim(), "agy.exe");
            if (File.Exists(candidate))
            {
                _cachedAgyPath = candidate;
                return candidate;
            }
        }

        return null;
    }

    public bool IsWindowsTerminalAvailable()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wtAlias = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(wtAlias)) return true;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return pathEnv.Split(Path.PathSeparator).Any(p => File.Exists(Path.Combine(p.Trim(), "wt.exe")));
    }

    public Task<Process?> LaunchProfileAsync(AccountProfile profile, TerminalType terminal, bool forceLoginPrompt = false)
    {
        return Task.Run(() =>
        {
            var effectiveDir = profile.GetEffectiveProfileDirectory();
            if (!Directory.Exists(effectiveDir))
            {
                Directory.CreateDirectory(effectiveDir);
            }

            var workingDir = GetValidWorkingDirectory(profile);
            var agyBinary = FindAgyExecutablePath() ?? "agy";
            var extraArgs = profile.ExtraArguments?.Trim() ?? string.Empty;
            var title = $"AGY [{profile.Name}]";

            // If user asked to force login or test auth, we can append a prompt hint or let it run
            var agyCommand = string.IsNullOrEmpty(extraArgs) ? agyBinary : $"{agyBinary} {extraArgs}";

            ProcessStartInfo psi;

            // Check if Windows Terminal is requested and available
            if (terminal == TerminalType.WindowsTerminal && IsWindowsTerminalAvailable())
            {
                // In wt.exe, we can launch cmd or powershell tab
                var cmdInner = $"title {title} && set USERPROFILE={effectiveDir} && set HOME={effectiveDir} && cd /d \"{workingDir}\" && {agyCommand}";
                psi = new ProcessStartInfo
                {
                    FileName = "wt.exe",
                    Arguments = $"--title \"{title}\" -d \"{workingDir}\" cmd.exe /k \"{cmdInner}\"",
                    UseShellExecute = true,
                    WorkingDirectory = workingDir
                };
            }
            else if (terminal == TerminalType.PowerShell)
            {
                var psScript = $"$host.UI.RawUI.WindowTitle = '{title}'; " +
                               $"$env:USERPROFILE = '{effectiveDir}'; " +
                               $"$env:HOME = '{effectiveDir}'; " +
                               $"Set-Location '{workingDir}'; " +
                               $"& '{agyBinary}' {extraArgs}";

                psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"{psScript}\"",
                    UseShellExecute = true,
                    WorkingDirectory = workingDir
                };
            }
            else
            {
                // Default Command Prompt
                var cmdArgs = $"/k \"title {title} && set USERPROFILE={effectiveDir} && set HOME={effectiveDir} && cd /d \"{workingDir}\" && {agyCommand}\"";
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    UseShellExecute = true,
                    WorkingDirectory = workingDir
                };
            }

            // Set environment variables directly on the parent ProcessStartInfo as well
            psi.Environment["USERPROFILE"] = effectiveDir;
            psi.Environment["HOME"] = effectiveDir;
            if (effectiveDir.Length >= 2 && effectiveDir[1] == ':')
            {
                psi.Environment["HOMEDRIVE"] = effectiveDir[..2];
                psi.Environment["HOMEPATH"] = effectiveDir[2..];
            }

            profile.LastLaunchedAt = DateTime.UtcNow;
            return Process.Start(psi);
        });
    }

    public async Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, bool splitPanes)
    {
        var profileList = profiles.ToList();
        var processes = new List<Process>();

        if (profileList.Count == 0) return processes;

        // If Windows Terminal is selected, available, and splitPanes is requested
        if (terminal == TerminalType.WindowsTerminal && IsWindowsTerminalAvailable() && splitPanes && profileList.Count > 1)
        {
            var wtArgs = new List<string>();

            for (int i = 0; i < profileList.Count; i++)
            {
                var p = profileList[i];
                var dir = p.GetEffectiveProfileDirectory();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var workDir = GetValidWorkingDirectory(p);
                var agyBinary = FindAgyExecutablePath() ?? "agy";
                var title = $"AGY [{p.Name}]";
                var innerCmd = $"title {title} && set USERPROFILE={dir} && set HOME={dir} && cd /d \"{workDir}\" && {agyBinary} {p.ExtraArguments?.Trim()}";

                if (i == 0)
                {
                    // First pane / tab
                    wtArgs.Add($"--title \"{title}\" -d \"{workDir}\" cmd.exe /k \"{innerCmd}\"");
                }
                else
                {
                    // Subsequent panes: split pane vertical or horizontal
                    var splitFlag = (i % 2 == 1) ? "-V" : "-H";
                    wtArgs.Add($"; split-pane {splitFlag} --title \"{title}\" -d \"{workDir}\" cmd.exe /k \"{innerCmd}\"");
                }

                p.LastLaunchedAt = DateTime.UtcNow;
            }

            var combinedArgs = string.Join(" ", wtArgs);
            var psi = new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = combinedArgs,
                UseShellExecute = true
            };

            var proc = Process.Start(psi);
            if (proc != null) processes.Add(proc);
            return processes;
        }

        // Sequential individual window launch
        foreach (var profile in profileList)
        {
            var proc = await LaunchProfileAsync(profile, terminal);
            if (proc != null) processes.Add(proc);
            // Slight delay so window positions don't perfectly overlap
            await Task.Delay(300);
        }

        return processes;
    }

    public string GetCliSnippet(AccountProfile profile, TerminalType terminal)
    {
        var effectiveDir = profile.GetEffectiveProfileDirectory();
        var workDir = GetValidWorkingDirectory(profile);
        var extraArgs = string.IsNullOrWhiteSpace(profile.ExtraArguments) ? "" : $" {profile.ExtraArguments.Trim()}";

        return terminal switch
        {
            TerminalType.PowerShell =>
                $"$env:USERPROFILE=\"{effectiveDir}\"; $env:HOME=\"{effectiveDir}\"; cd \"{workDir}\"; agy{extraArgs}",
            _ =>
                $"set \"USERPROFILE={effectiveDir}\" && set \"HOME={effectiveDir}\" && cd /d \"{workDir}\" && agy{extraArgs}"
        };
    }

    public void OpenProfileFolder(AccountProfile profile)
    {
        var dir = profile.GetEffectiveProfileDirectory();
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{dir}\"",
            UseShellExecute = true
        });
    }

    public void OpenWorkspaceFolder(AccountProfile profile)
    {
        var dir = GetValidWorkingDirectory(profile);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{dir}\"",
            UseShellExecute = true
        });
    }

    private static string GetValidWorkingDirectory(AccountProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.DefaultWorkspace))
        {
            var expanded = Environment.ExpandEnvironmentVariables(profile.DefaultWorkspace);
            if (Directory.Exists(expanded)) return expanded;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
