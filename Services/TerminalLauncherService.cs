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

        _cachedAgyPath = ResolveAgyExecutablePath();
        return _cachedAgyPath;
    }

    public static string? ResolveAgyExecutablePath()
    {
        // 1. Common install location for Antigravity CLI on Windows
        var userLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var standardPath = Path.Combine(userLocal, "agy", "bin", "agy.exe");
        if (File.Exists(standardPath))
        {
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
        var rawExtra = profile.ExtraArguments;
        var extraArgs = string.IsNullOrWhiteSpace(rawExtra) ? "" : " " + SanitizeCommandLineArgs(rawExtra);
        if (profile.DangerouslySkipPermissions && !extraArgs.Contains("--dangerously-skip-permissions"))
        {
            extraArgs = " --dangerously-skip-permissions" + extraArgs;
        }
        var rawTitle = $"AGY [{profile.Name}]";
        var safeTitle = SanitizeBatchString(rawTitle);
        var safeWorkDir = workDir.Replace("\"", "");
        var safeAgyBinary = agyBinary.Replace("\"", "");

        var scriptPath = Path.Combine(effectiveDir, "run-agy.cmd");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine($"title {safeTitle}");
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
        else
        {
            // Explicitly clear SSH markers so the default main profile ALWAYS uses
            // the system Windows Credential Manager and never gets tricked into thinking it's an SSH session
            sb.AppendLine("set \"SSH_CONNECTION=\"");
            sb.AppendLine("set \"SSH_CLIENT=\"");
        }

        sb.AppendLine($"cd /d \"{safeWorkDir}\"");
        sb.AppendLine("if /i \"%~1\"==\"--cli-only\" goto :cli_only");
        sb.AppendLine($"\"{safeAgyBinary}\"{extraArgs} %*");
        sb.AppendLine("goto :eof");
        sb.AppendLine();
        sb.AppendLine(":cli_only");
        sb.AppendLine($"echo [AGY Sandbox Shell - Profile: {safeTitle}]");
        sb.AppendLine("echo Environment variables isolated. Ready for 'agy' or 'agy -p \"your prompt\"'.");
        sb.AppendLine("echo.");
        sb.AppendLine("goto :eof");

        File.WriteAllText(scriptPath, sb.ToString(), new System.Text.UTF8Encoding(false));

        // Cross-Platform POSIX launcher for Linux / macOS / WSL
        try
        {
            var shPath = Path.Combine(effectiveDir, "run-agy.sh");
            var shSb = new System.Text.StringBuilder();
            shSb.AppendLine("#!/usr/bin/env bash");
            shSb.AppendLine($"# AGY Sandbox Shell - Profile: {profile.Name}");
            shSb.AppendLine($"export USERPROFILE=\"{effectiveDir.Replace('\\', '/')}\"");
            shSb.AppendLine($"export HOME=\"{effectiveDir.Replace('\\', '/')}\"");
            shSb.AppendLine($"export ANTIGRAVITY_APP_DATA_DIR=\"{effectiveDir.Replace('\\', '/')}/.gemini/antigravity-cli\"");
            shSb.AppendLine($"export JETSKI_APP_DATA_DIR=\"{effectiveDir.Replace('\\', '/')}/.gemini/antigravity-cli\"");
            if (!profile.IsMainDefaultProfile())
            {
                shSb.AppendLine("export SSH_CONNECTION=1");
                shSb.AppendLine("export SSH_CLIENT=1");
            }
            else
            {
                shSb.AppendLine("unset SSH_CONNECTION");
                shSb.AppendLine("unset SSH_CLIENT");
            }
            shSb.AppendLine($"cd \"{safeWorkDir.Replace('\\', '/')}\" || exit 1");
            shSb.AppendLine("if [ \"$1\" = \"--cli-only\" ]; then");
            shSb.AppendLine($"    echo \"[AGY Sandbox Shell - Profile: {profile.Name}]\"");
            shSb.AppendLine("    echo \"Environment variables isolated. Ready for agy or agy -p <prompt>.\"");
            shSb.AppendLine("    exec \"${SHELL:-bash}\"");
            shSb.AppendLine("else");
            shSb.AppendLine($"    exec agy{extraArgs} \"$@\"");
            shSb.AppendLine("fi");
            File.WriteAllText(shPath, shSb.ToString(), new System.Text.UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Logger.Warn($"[TerminalLauncher] Could not write run-agy.sh: {ex.Message}");
        }

        Logger.Info($"[TerminalLauncher] Ensured launcher script for '{profile.Name}' at '{scriptPath}' (Target: '{safeAgyBinary}', Isolated: {!profile.IsMainDefaultProfile()})");
        return scriptPath;
    }

    public static string BuildCmdArguments(string scriptPath)
    {
        var cleanPath = scriptPath.Replace("\"", "");
        return $"/k call \"{cleanPath}\"";
    }

    public static string BuildWindowsTerminalArguments(string title, string workingDir, string scriptPath)
    {
        var cleanTitle = System.Text.RegularExpressions.Regex.Replace(title.Replace("\"", "").Replace(";", " - "), @"\s+", " ").Trim();
        var cleanWorkDir = workingDir.Replace("\"", "");
        var cleanScript = scriptPath.Replace("\"", "");
        return $"--title \"{cleanTitle}\" -d \"{cleanWorkDir}\" cmd.exe /k call \"{cleanScript}\"";
    }

    public static string BuildPowerShellCommand(string title, string effectiveDir, string workingDir, string agyBinary, string? extraArgs, bool isIsolated = false, bool isCliOnly = false)
    {
        var cleanTitle = title.Replace("'", "''").Replace("\"", "").Replace("\r", "").Replace("\n", "");
        var cleanEffectiveDir = effectiveDir.Replace("'", "''").Replace("\"", "");
        var cleanWorkingDir = workingDir.Replace("'", "''").Replace("\"", "");
        var cleanAgy = agyBinary.Replace("'", "''").Replace("\"", "");
        var cleanExtra = string.IsNullOrWhiteSpace(extraArgs) ? "" : " " + SanitizeCommandLineArgs(extraArgs);

        var isolationSnippet = isIsolated
            ? "$env:SSH_CONNECTION = '1'; $env:SSH_CLIENT = '1'; "
            : "$env:SSH_CONNECTION = $null; $env:SSH_CLIENT = $null; Remove-Item Env:SSH_CONNECTION -ErrorAction SilentlyContinue; Remove-Item Env:SSH_CLIENT -ErrorAction SilentlyContinue; ";

        var launchCmd = isCliOnly
            ? $"Write-Host '[AGY Sandbox Shell - Profile: {cleanTitle}]' -ForegroundColor Cyan; Write-Host 'Environment variables isolated. Ready for agy or agy -p <prompt>.' -ForegroundColor Gray;"
            : $"& '{cleanAgy}'{cleanExtra}";

        return $"$host.UI.RawUI.WindowTitle = '{cleanTitle}'; " +
               $"$env:USERPROFILE = '{cleanEffectiveDir}'; " +
               $"$env:HOME = '{cleanEffectiveDir}'; " +
               $"$env:ANTIGRAVITY_APP_DATA_DIR = '{cleanEffectiveDir}\\.gemini\\antigravity-cli'; " +
               $"$env:JETSKI_APP_DATA_DIR = '{cleanEffectiveDir}\\.gemini\\antigravity-cli'; " +
               isolationSnippet +
               $"Set-Location '{cleanWorkingDir}'; " +
               launchCmd;
    }

    public static string SanitizeBatchString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var banned = new[] { '&', '|', '<', '>', '^', '"', '%', '(', ')', '\r', '\n' };
        var chars = input.Where(c => !banned.Contains(c)).ToArray();
        return new string(chars).Trim();
    }

    public static string SanitizeCommandLineArgs(string? args)
    {
        if (string.IsNullOrWhiteSpace(args)) return string.Empty;
        var banned = new[] { ';', '&', '|', '`', '$', '\r', '\n' };
        var chars = args.Where(c => !banned.Contains(c)).ToArray();
        return new string(chars).Trim();
    }

    public static string? SanitizeSessionArgs(string? sessionArgs)
    {
        if (string.IsNullOrWhiteSpace(sessionArgs)) return null;
        var trimmed = sessionArgs.Trim();
        if (trimmed.Equals("--continue", StringComparison.OrdinalIgnoreCase))
        {
            return "--continue";
        }
        if (trimmed.Equals("--cli-only", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("__cli_only__", StringComparison.OrdinalIgnoreCase))
        {
            return "--cli-only";
        }
        if (trimmed.StartsWith("--conversation ", StringComparison.OrdinalIgnoreCase))
        {
            var idPart = trimmed.Substring("--conversation ".Length).Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(idPart, @"^[a-zA-Z0-9_\-]+$"))
            {
                return $"--conversation {idPart}";
            }
        }
        return null;
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
            var extraArgs = SanitizeCommandLineArgs(profile.ExtraArguments);
            if (profile.DangerouslySkipPermissions && !extraArgs.Contains("--dangerously-skip-permissions"))
            {
                extraArgs = string.IsNullOrWhiteSpace(extraArgs) ? "--dangerously-skip-permissions" : $"--dangerously-skip-permissions {extraArgs}";
            }
            var safeSession = SanitizeSessionArgs(sessionArgs);
            bool isCliOnly = safeSession == "--cli-only";

            if (!string.IsNullOrWhiteSpace(safeSession) && !isCliOnly)
            {
                extraArgs = string.IsNullOrWhiteSpace(extraArgs) ? safeSession : $"{extraArgs} {safeSession}";
            }

            var title = $"AGY [{profile.Name}]";
            var scriptPath = EnsureLauncherScript(profile);
            var scriptCallSuffix = string.IsNullOrWhiteSpace(safeSession) ? "" : " " + safeSession;

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
                var psScript = BuildPowerShellCommand(title, effectiveDir, workingDir, agyBinary, extraArgs, !profile.IsMainDefaultProfile(), isCliOnly);
                var encodedCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psScript));

                psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -EncodedCommand {encodedCommand}",
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
            Logger.Info($"[TerminalLauncher] Launched profile '{profile.Name}' via {terminal} (PID: {proc?.Id.ToString() ?? "detached"}) with args '{safeSession ?? "none"}' in '{workingDir}'");
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
                    var cleanTitle = System.Text.RegularExpressions.Regex.Replace(title.Replace("\"", "").Replace(";", " - "), @"\s+", " ").Trim();
                    var cleanWorkDir = workDir.Replace("\"", "");
                    var cleanScript = scriptPath.Replace("\"", "");

                    if (swarmMode == SwarmLaunchMode.SplitPanes)
                    {
                        var splitFlag = (i % 2 == 1) ? "-V" : "-H";
                        wtArgs.Add($"; split-pane {splitFlag} --title \"{cleanTitle}\" -d \"{cleanWorkDir}\" cmd.exe /k call \"{cleanScript}\"");
                    }
                    else
                    {
                        wtArgs.Add($"; new-tab --title \"{cleanTitle}\" -d \"{cleanWorkDir}\" cmd.exe /k call \"{cleanScript}\"");
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
        var rawExtra = profile.ExtraArguments?.Trim();
        var extraArgs = string.IsNullOrWhiteSpace(rawExtra) ? "" : $" {rawExtra}";
        if (profile.DangerouslySkipPermissions && !extraArgs.Contains("--dangerously-skip-permissions"))
        {
            extraArgs = $" --dangerously-skip-permissions{extraArgs}";
        }
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
