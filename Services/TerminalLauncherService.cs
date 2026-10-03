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
        var binName = OperatingSystem.IsWindows() ? "agy.exe" : "agy";

        // 1. Common install locations
        if (OperatingSystem.IsWindows())
        {
            var userLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var standardPath = Path.Combine(userLocal, "agy", "bin", binName);
            if (File.Exists(standardPath))
            {
                return standardPath;
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var unixCandidates = new[]
            {
                Path.Combine(home, ".local", "bin", binName),
                Path.Combine(home, ".gemini", "bin", binName),
                Path.Combine(home, "bin", binName),
                Path.Combine("/usr", "local", "bin", binName),
                Path.Combine("/usr", "bin", binName),
                Path.Combine("/bin", binName),
                Path.Combine("/opt", "homebrew", "bin", binName),
                Path.Combine("/usr", "local", "Homebrew", "bin", binName)
            };
            foreach (var candidate in unixCandidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        // 2. Check in PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            var candidate = Path.Combine(p.Trim(), binName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public bool IsWindowsTerminalAvailable()
    {
        if (!OperatingSystem.IsWindows()) return false;

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
        EnsureProfileSettingsJson(effectiveDir, workDir);

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
        sb.AppendLine("echo.");
        sb.AppendLine("echo [AGY Swarm Session Finished]");
        sb.AppendLine("goto :eof");
        sb.AppendLine();
        sb.AppendLine(":cli_only");
        sb.AppendLine($"echo [AGY Sandbox Shell - Profile: {safeTitle}]");
        sb.AppendLine("echo Environment variables isolated. Ready for 'agy' or 'agy -p \"your prompt\"'.");
        sb.AppendLine("echo.");
        sb.AppendLine("goto :eof");

        File.WriteAllText(scriptPath, sb.ToString(), new System.Text.UTF8Encoding(false));

        // Cross-Platform POSIX launcher for Linux / macOS / WSL
        var shPath = Path.Combine(effectiveDir, "run-agy.sh");
        try
        {
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
            shSb.AppendLine($"    exec \"{safeAgyBinary.Replace('\\', '/')}\"{extraArgs} \"$@\"");
            shSb.AppendLine("fi");
            File.WriteAllText(shPath, shSb.ToString(), new System.Text.UTF8Encoding(false));

            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    File.SetUnixFileMode(shPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                catch (Exception pex)
                {
                    Logger.Debug($"[TerminalLauncher] SetUnixFileMode skipped: {pex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[TerminalLauncher] Could not write run-agy.sh: {ex.Message}");
        }

        var returnedScript = OperatingSystem.IsWindows() ? scriptPath : shPath;
        Logger.Info($"[TerminalLauncher] Ensured launcher script for '{profile.Name}' at '{returnedScript}' (Target: '{safeAgyBinary}', Isolated: {!profile.IsMainDefaultProfile()})");
        return returnedScript;
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

    public static void EnsureProfileSettingsJson(string effectiveDir, string? workspace = null)
    {
        try
        {
            var geminiDir = Path.Combine(effectiveDir, ".gemini", "antigravity-cli");
            if (!Directory.Exists(geminiDir))
            {
                Directory.CreateDirectory(geminiDir);
            }

            var settingsPath = Path.Combine(geminiDir, "settings.json");
            System.Text.Json.Nodes.JsonObject root;

            if (File.Exists(settingsPath))
            {
                try
                {
                    var content = File.ReadAllText(settingsPath);
                    root = System.Text.Json.Nodes.JsonNode.Parse(content) as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
                }
                catch
                {
                    root = new System.Text.Json.Nodes.JsonObject();
                }
            }
            else
            {
                root = new System.Text.Json.Nodes.JsonObject();
            }

            // Ensure permissions.allow contains all requisite permissions
            System.Text.Json.Nodes.JsonObject permissionsObj;
            if (root.TryGetPropertyValue("permissions", out var pNode) && pNode is System.Text.Json.Nodes.JsonObject pObj)
            {
                permissionsObj = pObj;
            }
            else
            {
                permissionsObj = new System.Text.Json.Nodes.JsonObject();
                root["permissions"] = permissionsObj;
            }

            System.Text.Json.Nodes.JsonArray allowArray;
            if (permissionsObj.TryGetPropertyValue("allow", out var aNode) && aNode is System.Text.Json.Nodes.JsonArray aArr)
            {
                allowArray = aArr;
            }
            else
            {
                allowArray = new System.Text.Json.Nodes.JsonArray();
                permissionsObj["allow"] = allowArray;
            }

            var defaultAllows = new[] { "command(*)", "file(*)", "run_command(*)", "*" };
            var existingAllows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in allowArray)
            {
                if (item != null && item.GetValue<string>() is { Length: > 0 } str)
                {
                    existingAllows.Add(str);
                }
            }

            foreach (var def in defaultAllows)
            {
                if (!existingAllows.Contains(def))
                {
                    allowArray.Add(def);
                }
            }

            // Ensure trustedWorkspaces if workspace is provided
            if (!string.IsNullOrWhiteSpace(workspace))
            {
                System.Text.Json.Nodes.JsonArray twArray;
                if (root.TryGetPropertyValue("trustedWorkspaces", out var twNode) && twNode is System.Text.Json.Nodes.JsonArray twArr)
                {
                    twArray = twArr;
                }
                else
                {
                    twArray = new System.Text.Json.Nodes.JsonArray();
                    root["trustedWorkspaces"] = twArray;
                }

                var cleanWs = workspace.Trim();
                bool exists = false;
                foreach (var item in twArray)
                {
                    if (item != null && string.Equals(item.GetValue<string>(), cleanWs, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    twArray.Add(cleanWs);
                }
            }

            var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(settingsPath, root.ToJsonString(jsonOptions), new System.Text.UTF8Encoding(false));
            Logger.Debug($"[TerminalLauncher] Ensured settings.json in '{geminiDir}' with allow-rules and trusted workspaces.");
        }
        catch (Exception ex)
        {
            Logger.Warn($"[TerminalLauncher] Failed to ensure settings.json in '{effectiveDir}': {ex.Message}");
        }
    }

    public static string? SanitizeSessionArgs(string? sessionArgs)
    {
        if (string.IsNullOrWhiteSpace(sessionArgs)) return null;
        var trimmed = sessionArgs.Trim();

        // 1. Detect and extract --dangerously-skip-permissions
        bool hasDangerouslySkip = false;
        const string skipFlag = "--dangerously-skip-permissions";
        if (trimmed.Contains(skipFlag, StringComparison.OrdinalIgnoreCase))
        {
            hasDangerouslySkip = true;
            int idx;
            while ((idx = trimmed.IndexOf(skipFlag, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                trimmed = (trimmed.Substring(0, idx) + " " + trimmed.Substring(idx + skipFlag.Length)).Trim();
            }
            trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ").Trim();
        }

        // If after removing flag it is empty
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return hasDangerouslySkip ? skipFlag : null;
        }

        // 2. Exact command aliases
        if (trimmed.Equals("--continue", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("-c", StringComparison.OrdinalIgnoreCase))
        {
            return hasDangerouslySkip ? $"{skipFlag} --continue" : "--continue";
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
                return hasDangerouslySkip ? $"{skipFlag} --conversation {idPart}" : $"--conversation {idPart}";
            }
            return null;
        }

        // 3. Prompt arguments: -p, --prompt, --print, -i, --prompt-interactive
        string? promptFlag = null;
        string rawPrompt = "";

        if (trimmed.StartsWith("--prompt-interactive=", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-i";
            rawPrompt = trimmed.Substring("--prompt-interactive=".Length);
        }
        else if (trimmed.StartsWith("--prompt-interactive ", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-i";
            rawPrompt = trimmed.Substring("--prompt-interactive ".Length);
        }
        else if (trimmed.StartsWith("-i\"", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-i";
            rawPrompt = trimmed.Substring(2);
        }
        else if (trimmed.StartsWith("-i ", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-i";
            rawPrompt = trimmed.Substring(3);
        }
        else if (trimmed.StartsWith("--prompt=", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "--prompt";
            rawPrompt = trimmed.Substring("--prompt=".Length);
        }
        else if (trimmed.StartsWith("--prompt ", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "--prompt";
            rawPrompt = trimmed.Substring("--prompt ".Length);
        }
        else if (trimmed.StartsWith("--print=", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-p";
            rawPrompt = trimmed.Substring("--print=".Length);
        }
        else if (trimmed.StartsWith("--print ", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-p";
            rawPrompt = trimmed.Substring("--print ".Length);
        }
        else if (trimmed.StartsWith("-p\"", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-p";
            rawPrompt = trimmed.Substring(2);
        }
        else if (trimmed.StartsWith("-p ", StringComparison.OrdinalIgnoreCase))
        {
            promptFlag = "-p";
            rawPrompt = trimmed.Substring(3);
        }

        if (promptFlag != null)
        {
            rawPrompt = rawPrompt.Trim();
            if (rawPrompt.StartsWith("\"") && rawPrompt.EndsWith("\"") && rawPrompt.Length >= 2)
            {
                rawPrompt = rawPrompt.Substring(1, rawPrompt.Length - 2);
            }
            else if (rawPrompt.StartsWith("'") && rawPrompt.EndsWith("'") && rawPrompt.Length >= 2)
            {
                rawPrompt = rawPrompt.Substring(1, rawPrompt.Length - 2);
            }

            // Sanitize batch and shell control characters
            var sanitized = rawPrompt
                .Replace("&", " and ")
                .Replace("|", " ")
                .Replace("<", " ")
                .Replace(">", " ")
                .Replace("^", " ")
                .Replace("%", " ")
                .Replace("`", "'")
                .Replace("\r", " ")
                .Replace("\n", " ");

            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s+", " ").Trim();
            sanitized = sanitized.Replace("\"", "'");

            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                return hasDangerouslySkip
                    ? $"{skipFlag} {promptFlag} \"{sanitized}\""
                    : $"{promptFlag} \"{sanitized}\"";
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
            EnsureProfileSettingsJson(effectiveDir, workingDir);

            var agyBinary = FindAgyExecutablePath() ?? "agy";
            var extraArgs = SanitizeCommandLineArgs(profile.ExtraArguments);
            if (profile.DangerouslySkipPermissions && !extraArgs.Contains("--dangerously-skip-permissions"))
            {
                extraArgs = string.IsNullOrWhiteSpace(extraArgs) ? "--dangerously-skip-permissions" : $"--dangerously-skip-permissions {extraArgs}";
            }
            var safeSession = SanitizeSessionArgs(sessionArgs);
            bool isCliOnly = safeSession == "--cli-only";

            if (!string.IsNullOrWhiteSpace(safeSession) &&
                (safeSession.StartsWith("-p") || safeSession.StartsWith("--prompt") || safeSession.StartsWith("-i") || safeSession.StartsWith("--prompt-interactive")) &&
                !extraArgs.Contains("--dangerously-skip-permissions") &&
                !safeSession.Contains("--dangerously-skip-permissions"))
            {
                extraArgs = string.IsNullOrWhiteSpace(extraArgs) ? "--dangerously-skip-permissions" : $"--dangerously-skip-permissions {extraArgs}";
            }

            if (!string.IsNullOrWhiteSpace(safeSession) && !isCliOnly)
            {
                extraArgs = string.IsNullOrWhiteSpace(extraArgs) ? safeSession : $"{extraArgs} {safeSession}";
            }

            var title = $"AGY [{profile.Name}]";
            var scriptPath = EnsureLauncherScript(profile);
            var scriptCallSuffix = string.IsNullOrWhiteSpace(safeSession) ? "" : " " + safeSession;

            // Cross-Platform POSIX launcher for Linux / macOS
            if (!OperatingSystem.IsWindows())
            {
                profile.LastLaunchedAt = DateTime.UtcNow;
                var posixProc = LaunchPosixTerminal(title, workingDir, scriptPath, safeSession);
                Logger.Info($"[TerminalLauncher] Launched POSIX profile '{profile.Name}' (PID: {posixProc?.Id.ToString() ?? "detached"}) with args '{safeSession ?? "none"}' in '{workingDir}'");
                return posixProc;
            }

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

    public Task<Process?> LaunchHeadlessAgyAsync(
        AccountProfile profile,
        string workingDir,
        string prompt,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        bool dangerouslySkipPermissions = true)
    {
        return Task.Run(() =>
        {
            var effectiveDir = profile.GetEffectiveProfileDirectory().Trim().TrimEnd('\\', '/');
            if (!Directory.Exists(effectiveDir))
            {
                Directory.CreateDirectory(effectiveDir);
            }

            var agyBinary = FindAgyExecutablePath() ?? "agy";
            var safeWorkDir = Directory.Exists(workingDir) ? workingDir : GetValidWorkingDirectory(profile);
            EnsureProfileSettingsJson(effectiveDir, safeWorkDir);

            // Clean prompt - keep readable while eliminating shell metacharacters
            var safePrompt = SanitizeCommandLineArgs(prompt)
                .Replace("&", " and ")
                .Replace("|", " ")
                .Replace("\"", "'")
                .Replace("\r", " ")
                .Replace("\n", " ");
            safePrompt = System.Text.RegularExpressions.Regex.Replace(safePrompt, @"\s+", " ").Trim();

            var argsList = new List<string>();
            if (dangerouslySkipPermissions || profile.DangerouslySkipPermissions)
            {
                argsList.Add("--dangerously-skip-permissions");
            }
            if (!string.IsNullOrWhiteSpace(profile.ExtraArguments))
            {
                var extra = SanitizeCommandLineArgs(profile.ExtraArguments);
                if (!string.IsNullOrWhiteSpace(extra)) argsList.Add(extra);
            }
            argsList.Add($"-p \"{safePrompt}\"");

            var arguments = string.Join(" ", argsList);

            var psi = new ProcessStartInfo
            {
                FileName = agyBinary,
                Arguments = arguments,
                WorkingDirectory = safeWorkDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            psi.EnvironmentVariables["USERPROFILE"] = effectiveDir;
            psi.EnvironmentVariables["HOME"] = effectiveDir;
            psi.EnvironmentVariables["ANTIGRAVITY_APP_DATA_DIR"] = Path.Combine(effectiveDir, ".gemini", "antigravity-cli");
            psi.EnvironmentVariables["JETSKI_APP_DATA_DIR"] = Path.Combine(effectiveDir, ".gemini", "antigravity-cli");

            if (!profile.IsMainDefaultProfile())
            {
                psi.EnvironmentVariables["SSH_CONNECTION"] = "1";
                psi.EnvironmentVariables["SSH_CLIENT"] = "1";
            }
            else
            {
                psi.EnvironmentVariables["SSH_CONNECTION"] = "";
                psi.EnvironmentVariables["SSH_CLIENT"] = "";
            }

            try
            {
                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                if (onOutputLine != null)
                {
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null) onOutputLine(e.Data);
                    };
                }
                if (onErrorLine != null)
                {
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null) onErrorLine(e.Data);
                    };
                }

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                profile.LastLaunchedAt = DateTime.UtcNow;
                Logger.Info($"[TerminalLauncher] Launched headless agy for '{profile.Name}' (PID: {proc.Id}) in '{safeWorkDir}'");
                return proc;
            }
            catch (Exception ex)
            {
                Logger.Error($"[TerminalLauncher] Failed to launch headless agy for '{profile.Name}'", ex);
                return null;
            }
        });
    }

    public async Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, SwarmLaunchMode swarmMode)
    {
        var profileList = profiles.ToList();
        var processes = new List<Process>();

        if (profileList.Count == 0) return processes;

        Logger.Info($"[TerminalLauncher] Orchestrating Swarm launch for {profileList.Count} accounts (Mode: {swarmMode}, Terminal: {terminal})");

        // If Windows Terminal is available and user chose SplitPanes or SeparateTabs on Windows
        if (OperatingSystem.IsWindows() && terminal == TerminalType.WindowsTerminal && IsWindowsTerminalAvailable() && swarmMode != SwarmLaunchMode.SeparateWindows && profileList.Count > 1)
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

        if (!OperatingSystem.IsWindows())
        {
            var cleanSafeWorkDir = workDir.Replace('\\', '/');
            var cleanSafeEffDir = effectiveDir.Replace('\\', '/');
            return (isIsolated ? "export SSH_CONNECTION=1 SSH_CLIENT=1 && " : "") +
                   $"export USERPROFILE=\"{cleanSafeEffDir}\" HOME=\"{cleanSafeEffDir}\" ANTIGRAVITY_APP_DATA_DIR=\"{cleanSafeEffDir}/.gemini/antigravity-cli\" JETSKI_APP_DATA_DIR=\"{cleanSafeEffDir}/.gemini/antigravity-cli\" && " +
                   $"cd \"{cleanSafeWorkDir}\" && agy{extraArgs}";
        }

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
        OpenFolderInFileManager(dir);
        Logger.Info($"[TerminalLauncher] Opened profile directory in File Manager: '{dir}'");
    }

    public void OpenWorkspaceFolder(AccountProfile profile)
    {
        var dir = GetValidWorkingDirectory(profile);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        OpenFolderInFileManager(dir);
        Logger.Info($"[TerminalLauncher] Opened workspace directory in File Manager: '{dir}'");
    }

    public static void OpenFolderInFileManager(string dir)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[TerminalLauncher] Failed to open folder '{dir}': {ex.Message}");
        }
    }

    public static void OpenFileOrUrl(string pathOrUrl)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open", $"\"{pathOrUrl}\"") { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{pathOrUrl}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[TerminalLauncher] Failed to open file/url '{pathOrUrl}': {ex.Message}");
        }
    }

    public static Process? LaunchPosixTerminal(string title, string workingDir, string scriptPath, string? scriptArgs)
    {
        var cleanTitle = title.Replace("\"", "").Replace("'", "");
        var cleanWorkDir = workingDir.Replace('\\', '/');
        var cleanScript = scriptPath.Replace('\\', '/');
        var argSuffix = string.IsNullOrWhiteSpace(scriptArgs) ? "" : $" {scriptArgs}";
        var runCommand = $"\"{cleanScript}\"{argSuffix}";

        if (OperatingSystem.IsMacOS())
        {
            try
            {
                var appleScript = $"tell application \"Terminal\" to do script \"cd \\\"{cleanWorkDir}\\\" && {runCommand.Replace("\"", "\\\"")}\"";
                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    Arguments = $"-e \"{appleScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                return Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Warn($"[TerminalLauncher] macOS Terminal.app launch failed: {ex.Message}");
            }
        }

        // Linux terminal candidate matrix
        var customTerminal = Environment.GetEnvironmentVariable("TERMINAL");
        var candidates = new List<(string Exe, Func<ProcessStartInfo> Builder)>();

        if (!string.IsNullOrWhiteSpace(customTerminal))
        {
            candidates.Add((customTerminal, () => new ProcessStartInfo
            {
                FileName = customTerminal,
                Arguments = $"-e bash -c \"cd '{cleanWorkDir}' && '{cleanScript}'{argSuffix}; exec bash\"",
                WorkingDirectory = cleanWorkDir,
                UseShellExecute = false
            }));
        }

        // Debian/Ubuntu alternatives system
        candidates.Add(("x-terminal-emulator", () => new ProcessStartInfo
        {
            FileName = "x-terminal-emulator",
            Arguments = $"-e bash -c \"cd '{cleanWorkDir}' && '{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // Ptyxis (GNOME 45+ / Fedora 40+ modern default)
        candidates.Add(("ptyxis", () => new ProcessStartInfo
        {
            FileName = "ptyxis",
            Arguments = $"--working-directory=\"{cleanWorkDir}\" --title=\"{cleanTitle}\" -- bash -c \"'{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // GNOME Terminal
        candidates.Add(("gnome-terminal", () => new ProcessStartInfo
        {
            FileName = "gnome-terminal",
            Arguments = $"--working-directory=\"{cleanWorkDir}\" --title=\"{cleanTitle}\" -- bash -c \"'{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // KDE Konsole
        candidates.Add(("konsole", () => new ProcessStartInfo
        {
            FileName = "konsole",
            Arguments = $"--workdir \"{cleanWorkDir}\" -p tabtitle=\"{cleanTitle}\" -e bash -c \"'{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // XFCE Terminal
        candidates.Add(("xfce4-terminal", () => new ProcessStartInfo
        {
            FileName = "xfce4-terminal",
            Arguments = $"--working-directory=\"{cleanWorkDir}\" --title=\"{cleanTitle}\" -e \"bash -c '{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // Alacritty
        candidates.Add(("alacritty", () => new ProcessStartInfo
        {
            FileName = "alacritty",
            Arguments = $"--working-directory \"{cleanWorkDir}\" --title \"{cleanTitle}\" -e bash -c \"'{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // Kitty
        candidates.Add(("kitty", () => new ProcessStartInfo
        {
            FileName = "kitty",
            Arguments = $"--directory \"{cleanWorkDir}\" --title \"{cleanTitle}\" bash -c \"'{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // WezTerm
        candidates.Add(("wezterm", () => new ProcessStartInfo
        {
            FileName = "wezterm",
            Arguments = $"start --cwd \"{cleanWorkDir}\" bash -c \"'{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        // XTerm
        candidates.Add(("xterm", () => new ProcessStartInfo
        {
            FileName = "xterm",
            Arguments = $"-title \"{cleanTitle}\" -e bash -c \"cd '{cleanWorkDir}' && '{cleanScript}'{argSuffix}; exec bash\"",
            WorkingDirectory = cleanWorkDir,
            UseShellExecute = false
        }));

        foreach (var (exe, builder) in candidates)
        {
            if (IsExecutableInPath(exe))
            {
                try
                {
                    var psi = builder();
                    var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        Logger.Info($"[TerminalLauncher] Successfully dispatched terminal via '{exe}'");
                        return proc;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[TerminalLauncher] Candidate '{exe}' failed: {ex.Message}");
                }
            }
        }

        // Generic fallback: execute via bash
        try
        {
            var fallbackPsi = new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = $"-c \"cd '{cleanWorkDir}' && '{cleanScript}'{argSuffix}\"",
                WorkingDirectory = cleanWorkDir,
                UseShellExecute = true
            };
            return Process.Start(fallbackPsi);
        }
        catch (Exception ex)
        {
            Logger.Error("[TerminalLauncher] All terminal launch attempts failed", ex);
            return null;
        }
    }

    public static bool IsExecutableInPath(string exeName)
    {
        if (File.Exists(exeName)) return true;
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            var candidate = Path.Combine(p.Trim(), exeName);
            if (File.Exists(candidate)) return true;
        }
        return false;
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
