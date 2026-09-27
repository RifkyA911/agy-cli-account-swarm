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
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine($"title {title}");
        sb.AppendLine($"set \"USERPROFILE={effectiveDir}\"");
        sb.AppendLine($"set \"HOME={effectiveDir}\"");
        sb.AppendLine($"set \"ANTIGRAVITY_APP_DATA_DIR={effectiveDir}\\.gemini\\antigravity-cli\"");
        sb.AppendLine($"set \"JETSKI_APP_DATA_DIR={effectiveDir}\\.gemini\\antigravity-cli\"");

        if (!profile.IsMainDefaultProfile())
        {
            // Bypasses the OS-wide Windows Credential Manager fallback (gemini:antigravity)
            // via agy's built-in keyring_detector_ssh. This guarantees that worker profiles
            // do not inherit the primary user's account and can independently authenticate.
            sb.AppendLine("set \"SSH_CONNECTION=1\"");
            sb.AppendLine("set \"SSH_CLIENT=1\"");
        }

        sb.AppendLine($"cd /d \"{workDir}\"");
        sb.AppendLine($"\"{agyBinary}\"{extraArgs} %*");

        File.WriteAllText(scriptPath, sb.ToString(), System.Text.Encoding.ASCII);
        Logger.Info($"[TerminalLauncher] Ensured launcher script for '{profile.Name}' at '{scriptPath}' (Target: '{agyBinary}', Isolated: {!profile.IsMainDefaultProfile()})");
        return scriptPath;
    }

    public static string BuildCmdArguments(string scriptPath)
    {
        return $"/k call \"{scriptPath}\"";
    }

    public static string BuildWindowsTerminalArguments(string title, string workingDir, string scriptPath)
    {
        return $"--title \"{title}\" -d \"{workingDir}\" cmd.exe /k call \"{scriptPath}\"";
    }

    public static string BuildPowerShellCommand(string title, string effectiveDir, string workingDir, string agyBinary, string? extraArgs, bool isIsolated = false)
    {
        var escapedTitle = title.Replace("'", "''");
        var escapedEffectiveDir = effectiveDir.Replace("'", "''");
        var escapedWorkingDir = workingDir.Replace("'", "''");
        var escapedAgy = agyBinary.Replace("'", "''");
        var cleanExtra = string.IsNullOrWhiteSpace(extraArgs) ? "" : " " + extraArgs.Trim();

        var isolationSnippet = isIsolated
            ? "$env:SSH_CONNECTION = '1'; $env:SSH_CLIENT = '1'; "
            : "";

        return $"$host.UI.RawUI.WindowTitle = '{escapedTitle}'; " +
               $"$env:USERPROFILE = '{escapedEffectiveDir}'; " +
               $"$env:HOME = '{escapedEffectiveDir}'; " +
               $"$env:ANTIGRAVITY_APP_DATA_DIR = '{escapedEffectiveDir}\\.gemini\\antigravity-cli'; " +
               $"$env:JETSKI_APP_DATA_DIR = '{escapedEffectiveDir}\\.gemini\\antigravity-cli'; " +
               isolationSnippet +
               $"Set-Location '{escapedWorkingDir}'; " +
               $"& '{escapedAgy}'{cleanExtra}";
    }

    public Task<Process?> LaunchProfileAsync(AccountProfile profile, TerminalType terminal, bool forceLoginPrompt = false, string? sessionArgs = null)
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
            if (!string.IsNullOrWhiteSpace(sessionArgs))
            {
                extraArgs = string.IsNullOrWhiteSpace(extraArgs) ? sessionArgs.Trim() : $"{extraArgs} {sessionArgs.Trim()}";
            }

            var title = $"AGY [{profile.Name}]";
            var scriptPath = EnsureLauncherScript(profile);
            var scriptCallSuffix = string.IsNullOrWhiteSpace(sessionArgs) ? "" : " " + sessionArgs.Trim();

            ProcessStartInfo psi;

            // Check if Windows Terminal is requested and available
            if (terminal == TerminalType.WindowsTerminal && IsWindowsTerminalAvailable())
            {
                psi = new ProcessStartInfo
                {
                    FileName = "wt.exe",
                    Arguments = BuildWindowsTerminalArguments(title, workingDir, scriptPath) + scriptCallSuffix,
                    UseShellExecute = true,
                    WorkingDirectory = workingDir
                };
            }
            else if (terminal == TerminalType.PowerShell)
            {
                var psScript = BuildPowerShellCommand(title, effectiveDir, workingDir, agyBinary, extraArgs, !profile.IsMainDefaultProfile());

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
                    Arguments = BuildCmdArguments(scriptPath) + scriptCallSuffix,
                    UseShellExecute = true,
                    WorkingDirectory = workingDir
                };
            }

            profile.LastLaunchedAt = DateTime.UtcNow;
            var proc = Process.Start(psi);
            Logger.Info($"[TerminalLauncher] Launched profile '{profile.Name}' via {terminal} (PID: {proc?.Id.ToString() ?? "detached"}) with args '{sessionArgs ?? "none"}' in '{workingDir}'");
            return proc;
        });
    }

    public async Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, SwarmLaunchMode swarmMode)
    {
        var profileList = profiles.ToList();
        var processes = new List<Process>();

        if (profileList.Count == 0) return processes;

        Logger.Info($"[TerminalLauncher] Orchestrating Swarm launch for {profileList.Count} accounts (Mode: {swarmMode}, Terminal: {terminal})");

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
                    wtArgs.Add(BuildWindowsTerminalArguments(title, workDir, scriptPath));
                }
                else
                {
                    if (swarmMode == SwarmLaunchMode.SplitPanes)
                    {
                        var splitFlag = (i % 2 == 1) ? "-V" : "-H";
                        wtArgs.Add($"; split-pane {splitFlag} --title \"{title}\" -d \"{workDir}\" cmd.exe /k call \"{scriptPath}\"");
                    }
                    else
                    {
                        wtArgs.Add($"; new-tab --title \"{title}\" -d \"{workDir}\" cmd.exe /k call \"{scriptPath}\"");
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
            Logger.Info($"[TerminalLauncher] Swarm Windows Terminal session started (PID: {proc?.Id.ToString() ?? "detached"})");
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
        bool isIsolated = !profile.IsMainDefaultProfile();

        string snippet = terminal switch
        {
            TerminalType.PowerShell =>
                (isIsolated ? "$env:SSH_CONNECTION=\"1\"; $env:SSH_CLIENT=\"1\"; " : "") +
                $"$env:USERPROFILE=\"{effectiveDir}\"; $env:HOME=\"{effectiveDir}\"; cd \"{workDir}\"; agy{extraArgs}",
            _ =>
                (isIsolated ? "set \"SSH_CONNECTION=1\" && set \"SSH_CLIENT=1\" && " : "") +
                $"set \"USERPROFILE={effectiveDir}\" && set \"HOME={effectiveDir}\" && cd /d \"{workDir}\" && agy{extraArgs}"
        };

        Logger.Info($"[TerminalLauncher] Generated CLI snippet for '{profile.Name}' ({terminal})");
        return snippet;
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
        Logger.Info($"[TerminalLauncher] Opened profile directory in Explorer: '{dir}'");
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
        Logger.Info($"[TerminalLauncher] Opened workspace directory in Explorer: '{dir}'");
    }

    public static string GetValidWorkingDirectory(AccountProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.DefaultWorkspace))
        {
            var expanded = Environment.ExpandEnvironmentVariables(profile.DefaultWorkspace);
            if (Directory.Exists(expanded)) return expanded;
        }

        if (profile.IsMainDefaultProfile())
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        // For isolated profiles, give them a dedicated workspace inside their profile sandbox
        var effectiveDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
        var workspaceDir = Path.Combine(effectiveDir, "workspace");
        if (!Directory.Exists(workspaceDir))
        {
            try
            {
                Directory.CreateDirectory(workspaceDir);
            }
            catch
            {
                return effectiveDir;
            }
        }
        return workspaceDir;
    }
}
