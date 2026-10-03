using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class FleetDispatcherService : IFleetDispatcherService
{
    private readonly IGitWorktreeService _gitWorktreeService;
    private readonly ITerminalLauncherService _terminalLauncherService;
    private readonly ISwarmAggregatorService _swarmAggregatorService;

    public FleetDispatcherService(
        IGitWorktreeService? gitWorktreeService = null,
        ITerminalLauncherService? terminalLauncherService = null,
        ISwarmAggregatorService? swarmAggregatorService = null)
    {
        _gitWorktreeService = gitWorktreeService ?? new GitWorktreeService();
        _terminalLauncherService = terminalLauncherService ?? new TerminalLauncherService();
        _swarmAggregatorService = swarmAggregatorService ?? new SwarmAggregatorService();
    }

    public async Task<ResourceCheckResult> CheckPreflightResourcesAsync(string workspacePath)
    {
        var result = new ResourceCheckResult();
        string targetPath = string.IsNullOrWhiteSpace(workspacePath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : workspacePath;

        try
        {
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            var root = Path.GetPathRoot(Path.GetFullPath(targetPath)) ?? (OperatingSystem.IsWindows() ? "C:\\" : "/");
            var driveInfo = new DriveInfo(root);
            result.AvailableDiskGb = Math.Round(driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 2);

            var memInfo = GC.GetGCMemoryInfo();
            result.AvailableMemoryGb = Math.Round(memInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0), 2);

            result.IsGitRepository = await _gitWorktreeService.IsGitRepositoryAsync(targetPath);
            if (result.IsGitRepository)
            {
                result.CurrentBranch = await _gitWorktreeService.GetCurrentBranchAsync(targetPath);
            }

            // OOR & Warning Guards
            if (result.AvailableDiskGb < 2.0)
            {
                result.IsSafe = false;
                result.ErrorMessage = $"Critical Disk Space: Only {result.AvailableDiskGb:F1} GB free. At least 2.0 GB required.";
            }
            else if (result.AvailableDiskGb < 5.0)
            {
                result.WarningMessage = $"Low Disk Space Warning: {result.AvailableDiskGb:F1} GB free remaining on drive.";
            }

            if (!result.IsGitRepository)
            {
                var fallbackMsg = "Non-Git Workspace Detected: Swarm will use dedicated sandbox workspaces without Git Worktrees.";
                result.WarningMessage = string.IsNullOrEmpty(result.WarningMessage)
                    ? fallbackMsg
                    : $"{result.WarningMessage} | {fallbackMsg}";
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[FleetDispatcher] Preflight check error: {ex.Message}");
            result.WarningMessage = $"Resource check partially failed: {ex.Message}";
        }

        return result;
    }

    public string SynthesizePrompt(
        string baseObjective,
        string role,
        DispatchMode mode,
        int workerIndex,
        int totalWorkers,
        SwarmProject? project = null,
        IEnumerable<AccountProfile>? allParticipatingWorkers = null)
    {
        if (string.IsNullOrWhiteSpace(baseObjective)) return string.Empty;

        if (project != null)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[SWARM CHAT - PROJECT: {project.Name.ToUpperInvariant()}]");
            sb.AppendLine($"Tech Stack: {project.TechStack}");
            if (!string.IsNullOrWhiteSpace(project.Description))
            {
                sb.AppendLine($"Context: {project.Description}");
            }
            sb.AppendLine($"Worker Assignment: {role} (Member {workerIndex + 1}/{totalWorkers})");

            if (allParticipatingWorkers != null)
            {
                var workersList = allParticipatingWorkers.ToList();
                if (workersList.Count > 1)
                {
                    sb.AppendLine("Fleet Roster:");
                    for (int wIdx = 0; wIdx < workersList.Count; wIdx++)
                    {
                        var w = workersList[wIdx];
                        var wRole = ResolveWorkerRole(w, wIdx);
                        sb.AppendLine($" - {w.Name}: {wRole}{(wIdx == workerIndex ? " (YOU)" : "")}");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine($"[OBJECTIVE]");
            sb.AppendLine(baseObjective.Trim());
            sb.AppendLine();
            sb.AppendLine($"[INTER-CLI COMMUNICATION & BLACKBOARD]");
            sb.AppendLine("- A shared `.swarm/bus.jsonl` message bus and `.swarm/blackboard.md` exist in the project root.");
            sb.AppendLine("- Consult `.swarm/blackboard.md` for shared API specifications and component contracts.");
            sb.AppendLine("- Document your architecture decisions and public signatures clearly so peers can coordinate.");
            sb.AppendLine();

            switch (mode)
            {
                case DispatchMode.Broadcast:
                    sb.AppendLine($"[DIRECTIVE: {role.ToUpperInvariant()}]");
                    sb.AppendLine("Implement all aspects of this objective relevant to your specialization.");
                    break;
                case DispatchMode.Consensus:
                    sb.AppendLine(workerIndex switch
                    {
                        0 => "[DIRECTIVE: Primary Implementer - Approach A]\nImplement the primary architecture cleanly using idiomatic patterns.",
                        1 => "[DIRECTIVE: Alternative Implementer - Approach B]\nImplement the architecture with alternate optimizations, resilience, and edge-case handling.",
                        _ => "[DIRECTIVE: QA Reviewer & Critic]\nReview code produced by peers, test for regressions, and provide unit tests and verification."
                    });
                    break;
                default:
                    sb.AppendLine($"[DIRECTIVE: {role.ToUpperInvariant()}]");
                    sb.AppendLine($"Focus on your designated responsibilities ({role}). Maintain modular boundaries.");
                    break;
            }

            return sb.ToString().Trim();
        }

        return mode switch
        {
            DispatchMode.Broadcast => baseObjective.Trim(),

            DispatchMode.Consensus => workerIndex switch
            {
                0 => $"[ROLE: Primary Implementer - Approach A]\nTask: {baseObjective.Trim()}\nInstruction: Implement this feature cleanly with idiomatic patterns.",
                1 => $"[ROLE: Alternative Implementer - Approach B]\nTask: {baseObjective.Trim()}\nInstruction: Implement this feature focusing on high performance, resilience, and edge-case handling.",
                _ => $"[ROLE: Code Reviewer & Critic]\nTask: {baseObjective.Trim()}\nInstruction: Inspect the implementation, review security, API conventions, and provide comprehensive unit tests."
            },

            _ => $"[ROLE: {role.ToUpperInvariant()}]\nTask Objective: {baseObjective.Trim()}\nInstruction: Focus exclusively on your domain ({role}). Keep changes modular and adhere to project standards."
        };
    }

    public async Task<List<DispatchedWorkerTask>> DispatchFleetAsync(
        FleetDispatchConfig config,
        IEnumerable<AccountProfile> selectedWorkers,
        TerminalType terminal,
        IProgress<FleetProgressReport>? progress = null,
        SwarmProject? project = null)
    {
        var workerList = selectedWorkers.ToList();
        var tasks = new List<DispatchedWorkerTask>();

        if (workerList.Count == 0 || string.IsNullOrWhiteSpace(config.TaskObjective))
        {
            return tasks;
        }

        if (project != null)
        {
            _swarmAggregatorService.EnsureProjectSwarmWorkspace(project, workerList);
            _ = _swarmAggregatorService.PostMessageAsync(project, new SwarmChatMessage
            {
                ProjectId = project.Id,
                SenderName = "Swarm Orchestrator",
                SenderRole = "System",
                SenderColor = "#10B981",
                Content = $"🚀 Swarm launched for project '{project.Name}' with {workerList.Count} worker(s). Mode: {config.Mode}. Execution: {config.ExecutionMode}.",
                Type = SwarmMessageType.SystemEvent
            });
        }

        progress?.Report(new FleetProgressReport
        {
            Percent = 10,
            Stage = "Validating Environment",
            Detail = $"Preparing dispatch for {workerList.Count} worker account(s)..."
        });

        var workspacePath = string.IsNullOrWhiteSpace(config.TargetWorkspace)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Path.GetFullPath(config.TargetWorkspace);

        bool isGit = await _gitWorktreeService.IsGitRepositoryAsync(workspacePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        progress?.Report(new FleetProgressReport
        {
            Percent = 20,
            Stage = isGit ? "Git Repository Confirmed" : "Dedicated Workspace Sandbox",
            Detail = isGit ? $"Base repository verified: {workspacePath}" : "Non-Git repository: using profile sandbox directories"
        });

        for (int i = 0; i < workerList.Count; i++)
        {
            var worker = workerList[i];
            var role = ResolveWorkerRole(worker, i);
            var tailoredPrompt = SynthesizePrompt(config.TaskObjective, role, config.Mode, i, workerList.Count, project, workerList);

            int workerBasePercent = 25 + (int)((i / (double)workerList.Count) * 65);

            progress?.Report(new FleetProgressReport
            {
                Percent = workerBasePercent,
                Stage = $"Synthesizing Objective for [{role}]",
                Detail = $"Worker {i + 1}/{workerList.Count}: {worker.Name} ({role})"
            });

            string targetWorkDir;
            string branchName = $"swarm/{SanitizeForBranch(worker.Name)}_{timestamp}";

            if (isGit && config.UseGitWorktrees)
            {
                progress?.Report(new FleetProgressReport
                {
                    Percent = workerBasePercent + 5,
                    Stage = "Allocating Git Worktree",
                    Detail = $"Branch: {branchName}"
                });

                var repoParent = Directory.GetParent(workspacePath)?.FullName ?? workspacePath;
                var repoName = new DirectoryInfo(workspacePath).Name;
                var worktreeBase = Path.Combine(repoParent, ".worktrees");
                if (!Directory.Exists(worktreeBase)) Directory.CreateDirectory(worktreeBase);

                var worktreePath = Path.Combine(worktreeBase, $"{repoName}-{SanitizeForBranch(worker.Name)}");

                var (success, _, finalPath) = await _gitWorktreeService.CreateWorktreeAsync(workspacePath, worktreePath, branchName);
                if (success)
                {
                    targetWorkDir = finalPath;
                }
                else
                {
                    Logger.Warn($"[FleetDispatcher] Worktree failed for {worker.Name}. Falling back to default workspace.");
                    targetWorkDir = project != null
                        ? _swarmAggregatorService.ResolveWorkerProjectDirectory(worker, project)
                        : TerminalLauncherService.GetValidWorkingDirectory(worker);
                }
            }
            else if (project != null)
            {
                targetWorkDir = _swarmAggregatorService.ResolveWorkerProjectDirectory(worker, project);
            }
            else
            {
                targetWorkDir = TerminalLauncherService.GetValidWorkingDirectory(worker);
            }

            var workerTask = new DispatchedWorkerTask
            {
                ProfileName = worker.Name,
                AssignedRole = role,
                TailoredPrompt = tailoredPrompt,
                WorktreePath = targetWorkDir,
                BranchName = branchName,
                Status = "Launching",
                StatusColor = "#3B82F6",
                StartedAt = DateTime.UtcNow,
                ExecutionMode = config.ExecutionMode,
                ColorTag = string.IsNullOrWhiteSpace(worker.ColorTag) ? "#4285F4" : worker.ColorTag,
                AvatarUrl = worker.AvatarUrl,
                AvatarInitial = !string.IsNullOrEmpty(worker.AvatarInitial) ? worker.AvatarInitial : "W",
                CurrentActivity = "Initializing session..."
            };

            // Temporarily set workspace for launch
            var originalWorkspace = worker.DefaultWorkspace;
            worker.DefaultWorkspace = targetWorkDir;

            TerminalLauncherService.EnsureProfileSettingsJson(worker.GetEffectiveProfileDirectory(), targetWorkDir);

            try
            {
                // Stagger launch to prevent CPU/IO spikes
                if (i > 0)
                {
                    await Task.Delay(600);
                }

                if (config.ExecutionMode == FleetExecutionMode.HeadlessSilent)
                {
                    progress?.Report(new FleetProgressReport
                    {
                        Percent = workerBasePercent + 15,
                        Stage = "Spawning Background Process",
                        Detail = $"Starting silent background agy for '{worker.Name}' in '{targetWorkDir}'"
                    });

                    workerTask.CurrentActivity = "Spawning background process...";
                    workerTask.Status = "Running";
                    workerTask.StatusColor = "#10B981";

                    var proc = await _terminalLauncherService.LaunchHeadlessAgyAsync(
                        worker,
                        targetWorkDir,
                        tailoredPrompt,
                        onOutputLine: line =>
                        {
                            workerTask.LastOutputLine = line;
                            if (string.IsNullOrWhiteSpace(workerTask.FullOutputLog))
                                workerTask.FullOutputLog = line;
                            else
                                workerTask.FullOutputLog += Environment.NewLine + line;

                            var activity = ParseActivityFromOutput(line);
                            if (!string.IsNullOrWhiteSpace(activity))
                            {
                                workerTask.CurrentActivity = activity;
                            }

                            if (project != null)
                            {
                                _ = _swarmAggregatorService.PostAgentActionTelemetryAsync(
                                    project,
                                    worker.Name,
                                    role,
                                    line,
                                    worker.ColorTag,
                                    worker.AvatarUrl,
                                    worker.AvatarInitial);
                            }
                        },
                        onErrorLine: err =>
                        {
                            workerTask.LastOutputLine = $"[ERR] {err}";
                            if (string.IsNullOrWhiteSpace(workerTask.FullOutputLog))
                                workerTask.FullOutputLog = $"[ERR] {err}";
                            else
                                workerTask.FullOutputLog += Environment.NewLine + $"[ERR] {err}";

                            var activity = ParseActivityFromOutput(err);
                            if (!string.IsNullOrWhiteSpace(activity))
                            {
                                workerTask.CurrentActivity = activity;
                            }
                        },
                        dangerouslySkipPermissions: config.DangerouslySkipPermissions);

                    if (proc != null)
                    {
                        workerTask.ProcessId = proc.Id;
                        workerTask.CurrentActivity = "Running agy headless...";
                        proc.EnableRaisingEvents = true;
                        proc.Exited += (s, e) =>
                        {
                            workerTask.CompletedAt = DateTime.UtcNow;
                            if (proc.ExitCode == 0)
                            {
                                workerTask.Status = "Completed";
                                workerTask.StatusColor = "#10B981";
                                workerTask.CurrentActivity = "✅ Completed successfully";
                            }
                            else
                            {
                                workerTask.Status = $"Exited ({proc.ExitCode})";
                                workerTask.StatusColor = proc.ExitCode == 0 ? "#10B981" : "#EF4444";
                                workerTask.CurrentActivity = $"⚠️ Process exited with code {proc.ExitCode}";
                            }

                            if (project != null)
                            {
                                _ = _swarmAggregatorService.PostMessageAsync(project, new SwarmChatMessage
                                {
                                    ProjectId = project.Id,
                                    SenderName = worker.Name,
                                    SenderRole = role,
                                    SenderColor = string.IsNullOrWhiteSpace(worker.ColorTag) ? "#10B981" : worker.ColorTag,
                                    AvatarUrl = worker.AvatarUrl,
                                    AvatarInitial = !string.IsNullOrEmpty(worker.AvatarInitial) ? worker.AvatarInitial : "W",
                                    Content = proc.ExitCode == 0 ? "✅ Task finished successfully" : $"⚠️ Process exited with code {proc.ExitCode}",
                                    Type = proc.ExitCode == 0 ? SwarmMessageType.Handoff : SwarmMessageType.SystemEvent
                                });
                            }
                        };
                    }
                    else
                    {
                        workerTask.Status = "Detached";
                        workerTask.StatusColor = "#6B7280";
                        workerTask.CurrentActivity = "Process spawned detached";
                    }
                }
                else
                {
                    progress?.Report(new FleetProgressReport
                    {
                        Percent = workerBasePercent + 15,
                        Stage = "Spawning Terminal Session",
                        Detail = $"Launching isolated session for '{worker.Name}' in '{targetWorkDir}'"
                    });

                    workerTask.CurrentActivity = "Opening interactive terminal window...";

                    // Prepare session arg with initial prompt and permission flag
                    var safePromptArg = EscapePromptForCli(tailoredPrompt);
                    var sessionArg = config.DangerouslySkipPermissions
                        ? $"--dangerously-skip-permissions -p \"{safePromptArg}\""
                        : $"-p \"{safePromptArg}\"";

                    var proc = await _terminalLauncherService.LaunchProfileAsync(
                        worker,
                        terminal,
                        false,
                        sessionArg);

                    if (proc != null)
                    {
                        workerTask.ProcessId = proc.Id;
                        workerTask.Status = "Running";
                        workerTask.StatusColor = "#10B981";
                        workerTask.CurrentActivity = "Interactive terminal active";

                        if (project != null)
                        {
                            _ = _swarmAggregatorService.PostMessageAsync(project, new SwarmChatMessage
                            {
                                ProjectId = project.Id,
                                SenderName = worker.Name,
                                SenderRole = role,
                                SenderColor = string.IsNullOrWhiteSpace(worker.ColorTag) ? "#4285F4" : worker.ColorTag,
                                AvatarUrl = worker.AvatarUrl,
                                AvatarInitial = !string.IsNullOrEmpty(worker.AvatarInitial) ? worker.AvatarInitial : "W",
                                Content = $"⚡ Interactive terminal session active in '{targetWorkDir}'",
                                Type = SwarmMessageType.SystemEvent
                            });
                        }
                    }
                    else
                    {
                        workerTask.Status = "Detached";
                        workerTask.StatusColor = "#6B7280";
                        workerTask.CurrentActivity = "Terminal spawned detached";
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[FleetDispatcher] Launch error for {worker.Name}", ex);
                workerTask.Status = $"Failed: {ex.Message}";
                workerTask.StatusColor = "#EF4444";
                workerTask.CurrentActivity = $"Failed: {ex.Message}";
            }
            finally
            {
                worker.DefaultWorkspace = originalWorkspace;
            }

            tasks.Add(workerTask);
        }

        progress?.Report(new FleetProgressReport
        {
            Percent = 100,
            Stage = "Fleet Dispatched Successfully",
            Detail = $"All {tasks.Count} worker instances running in isolated environments."
        });

        return tasks;
    }

    public static readonly HashSet<string> ShellProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "cmd.exe",
        "powershell", "powershell.exe",
        "pwsh", "pwsh.exe",
        "wt", "wt.exe",
        "windowsterminal", "windowsterminal.exe",
        "conhost", "conhost.exe",
        "openconsole", "openconsole.exe"
    };

    public async Task<bool> StopTaskAsync(DispatchedWorkerTask task)
    {
        if (task == null) return false;
        return await Task.Run(() =>
        {
            if (task.ProcessId.HasValue)
            {
                try
                {
                    var proc = Process.GetProcessById(task.ProcessId.Value);
                    if (!proc.HasExited)
                    {
                        proc.Kill(entireProcessTree: true);
                        Logger.Info($"[FleetDispatcher] Stopped task process tree PID {task.ProcessId} for '{task.ProfileName}'");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[FleetDispatcher] Root process {task.ProcessId} already exited or inaccessible: {ex.Message}");
                }

                // In addition, ensure all descendant processes (terminal windows, agy CLI instances) are terminated
                StopTerminalWorkerProcesses(task);
            }
            task.Status = "Stopped";
            task.StatusColor = "#EF4444";
            task.CurrentActivity = "🛑 Stopped by user";
            task.CompletedAt = DateTime.UtcNow;
            return true;
        });
    }

    public static void StopTerminalWorkerProcesses(DispatchedWorkerTask task)
    {
        int killedCount = 0;

        if (OperatingSystem.IsWindows() && task.ProcessId.HasValue)
        {
            try
            {
                var allProcs = Win32ProcessHelper.GetAllProcesses();
                var descendants = Win32ProcessHelper.GetDescendantProcesses(task.ProcessId.Value, allProcs);

                // Terminate all descendants (both terminal windows and agy worker processes)
                foreach (var procNode in descendants)
                {
                    try
                    {
                        var p = Process.GetProcessById(procNode.ProcessId);
                        if (!p.HasExited)
                        {
                            p.Kill(entireProcessTree: true);
                            killedCount++;
                            Logger.Info($"[FleetDispatcher] Terminated worker process/terminal '{procNode.Name}' (PID: {procNode.ProcessId}) for '{task.ProfileName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[FleetDispatcher] Process {procNode.ProcessId} already exited: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[FleetDispatcher] Win32ProcessHelper failed: {ex.Message}");
            }
        }
        else if (!OperatingSystem.IsWindows() && task.ProcessId.HasValue)
        {
            try
            {
                var p = Process.GetProcessById(task.ProcessId.Value);
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    killedCount++;
                    Logger.Info($"[FleetDispatcher] Terminated Unix process tree for PID {task.ProcessId.Value} ('{task.ProfileName}')");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"[FleetDispatcher] Unix process termination exception: {ex.Message}");
            }

            try
            {
                using var pkill = Process.Start(new ProcessStartInfo
                {
                    FileName = "pkill",
                    Arguments = $"-9 -P {task.ProcessId.Value}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                pkill?.WaitForExit(1000);
            }
            catch { }
        }

        // Safeguard: If no child was found via process tree (e.g. detached wt.exe tab),
        // check running 'agy' processes associated with this workspace or profile
        if (killedCount == 0 && !string.IsNullOrWhiteSpace(task.WorktreePath))
        {
            try
            {
                var agyProcesses = Process.GetProcessesByName("agy");
                foreach (var agyProc in agyProcesses)
                {
                    try
                    {
                        if (task.ProcessId.HasValue && Win32ProcessHelper.IsParentOrAncestor(task.ProcessId.Value, agyProc.Id))
                        {
                            agyProc.Kill(entireProcessTree: true);
                            killedCount++;
                            Logger.Info($"[FleetDispatcher] Terminated matching agy process (PID: {agyProc.Id}) for '{task.ProfileName}'");
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    public async Task<int> AbortFleetAsync(IEnumerable<DispatchedWorkerTask> activeTasks)
    {
        int killed = 0;
        foreach (var task in activeTasks)
        {
            if (task.Status == "Running" || task.Status == "Launching")
            {
                if (await StopTaskAsync(task))
                {
                    killed++;
                }
            }
        }
        return killed;
    }

    public static string ParseActivityFromOutput(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return string.Empty;
        var trimmed = line.Trim();

        if (trimmed.Contains("thinking", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Analyzing", StringComparison.OrdinalIgnoreCase))
            return "🧠 Thinking & analyzing...";
        if (trimmed.Contains("run_command", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Executing", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("$ ") || trimmed.StartsWith("> "))
            return "⚡ Executing command...";
        if (trimmed.Contains("view_file", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("read_", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Reading", StringComparison.OrdinalIgnoreCase))
            return "📖 Reading file / context...";
        if (trimmed.Contains("write_to_file", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("replace_file_content", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Writing", StringComparison.OrdinalIgnoreCase))
            return "✏️ Writing / editing code...";
        if (trimmed.Contains("search", StringComparison.OrdinalIgnoreCase))
            return "🔍 Searching codebase / tools...";
        if (trimmed.Contains("git", StringComparison.OrdinalIgnoreCase))
            return "🌿 Git operations...";
        if (trimmed.Contains("permission", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("denied", StringComparison.OrdinalIgnoreCase))
            return "⚠️ Permission required / denied";
        if (trimmed.Contains("Error", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("Exception", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("failed", StringComparison.OrdinalIgnoreCase))
            return $"⚠️ {(trimmed.Length > 60 ? trimmed.Substring(0, 57) + "..." : trimmed)}";
        if (trimmed.StartsWith("Turn ", StringComparison.OrdinalIgnoreCase))
            return $"🔄 {trimmed}";
        if (trimmed.Length > 3)
        {
            return $"⚙️ {(trimmed.Length > 70 ? trimmed.Substring(0, 67) + "..." : trimmed)}";
        }
        return string.Empty;
    }

    public static string ResolveWorkerRole(AccountProfile profile, int index)
    {
        var desc = profile.Description?.ToLowerInvariant() ?? "";
        if (desc.Contains("backend") || desc.Contains("api")) return "Backend Engineer";
        if (desc.Contains("frontend") || desc.Contains("ui")) return "Frontend Engineer";
        if (desc.Contains("test") || desc.Contains("qa")) return "QA & Test Specialist";
        if (desc.Contains("doc") || desc.Contains("writer")) return "Technical Writer";
        if (desc.Contains("review") || desc.Contains("audit")) return "Code Reviewer";

        return index switch
        {
            0 => "Core Implementer",
            1 => "Component Engineer",
            2 => "QA & Test Specialist",
            _ => "Reviewer & Validator"
        };
    }

    public static string SanitizeForBranch(string name)
    {
        var safe = name.ToLowerInvariant().Replace(' ', '-');
        var invalid = new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', '^', '~' };
        return string.Join("", safe.Where(c => !invalid.Contains(c))).Trim('-');
    }

    public static string EscapePromptForCli(string prompt)
    {
        return prompt.Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
