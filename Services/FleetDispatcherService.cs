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

    public FleetDispatcherService(
        IGitWorktreeService? gitWorktreeService = null,
        ITerminalLauncherService? terminalLauncherService = null)
    {
        _gitWorktreeService = gitWorktreeService ?? new GitWorktreeService();
        _terminalLauncherService = terminalLauncherService ?? new TerminalLauncherService();
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

    public string SynthesizePrompt(string baseObjective, string role, DispatchMode mode, int workerIndex, int totalWorkers)
    {
        if (string.IsNullOrWhiteSpace(baseObjective)) return string.Empty;

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
        IProgress<FleetProgressReport>? progress = null)
    {
        var workerList = selectedWorkers.ToList();
        var tasks = new List<DispatchedWorkerTask>();

        if (workerList.Count == 0 || string.IsNullOrWhiteSpace(config.TaskObjective))
        {
            return tasks;
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
            var tailoredPrompt = SynthesizePrompt(config.TaskObjective, role, config.Mode, i, workerList.Count);

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
                    targetWorkDir = TerminalLauncherService.GetValidWorkingDirectory(worker);
                }
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
                StartedAt = DateTime.UtcNow
            };

            // Temporarily set workspace for launch
            var originalWorkspace = worker.DefaultWorkspace;
            worker.DefaultWorkspace = targetWorkDir;

            try
            {
                // Stagger launch to prevent CPU/IO spikes
                if (i > 0)
                {
                    await Task.Delay(600);
                }

                progress?.Report(new FleetProgressReport
                {
                    Percent = workerBasePercent + 15,
                    Stage = "Spawning Terminal Session",
                    Detail = $"Launching isolated session for '{worker.Name}' in '{targetWorkDir}'"
                });

                // Prepare session arg with initial prompt
                var safePromptArg = EscapePromptForCli(tailoredPrompt);
                var proc = await _terminalLauncherService.LaunchProfileAsync(
                    worker,
                    terminal,
                    false,
                    $"-p \"{safePromptArg}\"");

                if (proc != null)
                {
                    workerTask.ProcessId = proc.Id;
                    workerTask.Status = "Running";
                    workerTask.StatusColor = "#10B981";
                }
                else
                {
                    workerTask.Status = "Detached";
                    workerTask.StatusColor = "#6B7280";
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[FleetDispatcher] Launch error for {worker.Name}", ex);
                workerTask.Status = $"Failed: {ex.Message}";
                workerTask.StatusColor = "#EF4444";
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

    public async Task<int> AbortFleetAsync(IEnumerable<DispatchedWorkerTask> activeTasks)
    {
        int killed = 0;
        await Task.Run(() =>
        {
            foreach (var task in activeTasks)
            {
                if (task.ProcessId.HasValue)
                {
                    try
                    {
                        var proc = Process.GetProcessById(task.ProcessId.Value);
                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: true);
                            killed++;
                        }
                    }
                    catch
                    {
                        // Ignore exited process errors
                    }
                }
                task.Status = "Stopped";
                task.StatusColor = "#EF4444";
            }
        });

        return killed;
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
