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

    public string EnsureLauncherScript(AccountProfile profile)
    {
        var effectiveDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
        if (!Directory.Exists(effectiveDir))
        {
            Directory.CreateDirectory(effectiveDir);
        }

        var workDir = GetValidWorkingDirectory(profile);
        var agyBinary = FindAgyExecutablePath() ?? "agy";
        var extraArgs = string.IsNullOrWhiteSpace(profile.ExtraArguments) ? "" : " " + profile.ExtraArguments.Trim();
        var title = $"AGY [{profile.Name}]";

        var scriptPath = Path.Combine(effectiveDir, "run-agy.cmd");
        var content = "@echo off\r\n" +
                      $"title {title}\r\n" +
                      $"set \"USERPROFILE={effectiveDir}\"\r\n" +
                      $"set \"HOME={effectiveDir}\"\r\n" +
                      $"cd /d \"{workDir}\"\r\n" +
                      $"\"{agyBinary}\"{extraArgs} %*\r\n";

        File.WriteAllText(scriptPath, content, System.Text.Encoding.ASCII);
        return scriptPath;
    }

    public Task<Process?> LaunchProfileAsync(AccountProfile profile, TerminalType terminal, bool forceLoginPrompt = false)
    {
        return Task.Run(() =>
        {
            var effectiveDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
            if (!Directory.Exists(effectiveDir))
            {
                Directory.CreateDirectory(effectiveDir);
            }

            var workingDir = GetValidWorkingDirectory(profile);
            var agyBinary = FindAgyExecutablePath() ?? "agy";
            var extraArgs = profile.ExtraArguments?.Trim() ?? string.Empty;
            var title = $"AGY [{profile.Name}]";
            var scriptPath = EnsureLauncherScript(profile);

            ProcessStartInfo psi;

            // Check if Windows Terminal is requested and available
            if (terminal == TerminalType.WindowsTerminal && IsWindowsTerminalAvailable())
            {
                psi = new ProcessStartInfo
                {
                    FileName = "wt.exe",
                    Arguments = $"--title \"{title}\" -d \"{workingDir}\" cmd.exe /k \"\"{scriptPath}\"\"",
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
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"\"{scriptPath}\"\"",
                    UseShellExecute = true,
                    WorkingDirectory = workingDir
                };
            }

            profile.LastLaunchedAt = DateTime.UtcNow;
            return Process.Start(psi);
        });
    }

    public async Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, SwarmLaunchMode swarmMode)
    {
        var profileList = profiles.ToList();
        var processes = new List<Process>();

        if (profileList.Count == 0) return processes;

        // If Windows Terminal is available and user chose SplitPanes or SeparateTabs
        if (terminal == TerminalType.WindowsTerminal && IsWindowsTerminalAvailable() && swarmMode != SwarmLaunchMode.SeparateWindows && profileList.Count > 1)
        {
            var wtArgs = new List<string>();

            for (int i = 0; i < profileList.Count; i++)
            {
                var p = profileList[i];
                var workDir = GetValidWorkingDirectory(p);
                var title = $"AGY [{p.Name}]";
                var scriptPath = EnsureLauncherScript(p);

                if (i == 0)
                {
                    wtArgs.Add($"--title \"{title}\" -d \"{workDir}\" cmd.exe /k \"\"{scriptPath}\"\"");
                }
                else
                {
                    if (swarmMode == SwarmLaunchMode.SplitPanes)
                    {
                        var splitFlag = (i % 2 == 1) ? "-V" : "-H";
                        wtArgs.Add($"; split-pane {splitFlag} --title \"{title}\" -d \"{workDir}\" cmd.exe /k \"\"{scriptPath}\"\"");
                    }
                    else
                    {
                        wtArgs.Add($"; new-tab --title \"{title}\" -d \"{workDir}\" cmd.exe /k \"\"{scriptPath}\"\"");
                    }
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

        // Multi-Window Launch mode (Separate independent terminal windows)
        foreach (var profile in profileList)
        {
            var proc = await LaunchProfileAsync(profile, terminal);
            if (proc != null) processes.Add(proc);
            await Task.Delay(300);
        }

        return processes;
    }

    public string GetCliSnippet(AccountProfile profile, TerminalType terminal)
    {
        var effectiveDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
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
        var dir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
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
