using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class GitWorktreeService : IGitWorktreeService
{
    public async Task<bool> IsGitRepositoryAsync(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return false;

        var (code, outText, _) = await RunGitCommandAsync(directory, "rev-parse --is-inside-work-tree");
        return code == 0 && outText.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string?> GetCurrentBranchAsync(string directory)
    {
        if (!await IsGitRepositoryAsync(directory)) return null;

        var (code, outText, _) = await RunGitCommandAsync(directory, "branch --show-current");
        if (code == 0 && !string.IsNullOrWhiteSpace(outText))
        {
            return outText.Trim();
        }

        var (code2, outText2, _) = await RunGitCommandAsync(directory, "rev-parse --abbrev-ref HEAD");
        return code2 == 0 ? outText2.Trim() : null;
    }

    public async Task<List<GitWorktreeInfo>> ListWorktreesAsync(string repoDir)
    {
        var list = new List<GitWorktreeInfo>();
        if (!await IsGitRepositoryAsync(repoDir)) return list;

        var (code, outText, _) = await RunGitCommandAsync(repoDir, "worktree list --porcelain");
        if (code != 0 || string.IsNullOrWhiteSpace(outText)) return list;

        return ParseWorktreePorcelain(outText);
    }

    public static List<GitWorktreeInfo> ParseWorktreePorcelain(string porcelainOutput)
    {
        var list = new List<GitWorktreeInfo>();
        if (string.IsNullOrWhiteSpace(porcelainOutput)) return list;

        var lines = porcelainOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        GitWorktreeInfo? current = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("worktree "))
            {
                if (current != null) list.Add(current);
                current = new GitWorktreeInfo { Path = line.Substring("worktree ".Length).Trim() };
            }
            else if (current != null)
            {
                if (line.StartsWith("HEAD "))
                {
                    current.CommitHash = line.Substring("HEAD ".Length).Trim();
                }
                else if (line.StartsWith("branch "))
                {
                    current.Branch = line.Substring("branch ".Length).Trim().Replace("refs/heads/", "");
                }
                else if (line.StartsWith("locked"))
                {
                    current.IsLocked = true;
                    var reason = line.Substring("locked".Length).Trim();
                    current.LockReason = string.IsNullOrEmpty(reason) ? "Locked" : reason;
                }
            }
        }

        if (current != null) list.Add(current);
        return list;
    }


    public async Task<(bool Success, string Output, string WorktreePath)> CreateWorktreeAsync(string repoDir, string worktreePath, string branchName)
    {
        if (!await IsGitRepositoryAsync(repoDir))
        {
            return (false, "Target directory is not a valid git repository", worktreePath);
        }

        var fullWorktreePath = Path.GetFullPath(worktreePath);
        var safeBranch = SanitizeBranchName(branchName);

        // Pre-clean stale folder if empty
        if (Directory.Exists(fullWorktreePath))
        {
            if (!Directory.EnumerateFileSystemEntries(fullWorktreePath).Any())
            {
                try { Directory.Delete(fullWorktreePath, true); } catch { }
            }
            else
            {
                return (false, $"Worktree path already exists and is not empty: {fullWorktreePath}", fullWorktreePath);
            }
        }

        var cmd = $"worktree add -b \"{safeBranch}\" \"{fullWorktreePath}\" HEAD";
        var (code, outText, errText) = await RunGitCommandAsync(repoDir, cmd);

        bool success = code == 0;
        string combined = string.IsNullOrWhiteSpace(errText) ? outText : $"{outText}\n{errText}".Trim();
        Logger.Info($"[GitWorktree] Create worktree at '{fullWorktreePath}' with branch '{safeBranch}' -> Code: {code}");

        return (success, combined, fullWorktreePath);
    }

    public async Task<(bool Success, string Output)> RemoveWorktreeAsync(string repoDir, string worktreePath, bool force = false)
    {
        if (!await IsGitRepositoryAsync(repoDir))
        {
            return (false, "Not a git repository");
        }

        var fullPath = Path.GetFullPath(worktreePath);
        var forceFlag = force ? " --force" : "";
        var (code, outText, errText) = await RunGitCommandAsync(repoDir, $"worktree remove{forceFlag} \"{fullPath}\"");

        // Prune metadata
        await RunGitCommandAsync(repoDir, "worktree prune");

        bool success = code == 0;
        string combined = string.IsNullOrWhiteSpace(errText) ? outText : $"{outText}\n{errText}".Trim();
        Logger.Info($"[GitWorktree] Remove worktree '{fullPath}' -> Code: {code}");

        return (success, combined);
    }

    public async Task<List<string>> ListSwarmBranchesAsync(string repoDir)
    {
        var branches = new List<string>();
        if (!await IsGitRepositoryAsync(repoDir)) return branches;

        var (code, outText, _) = await RunGitCommandAsync(repoDir, "branch --list \"swarm/*\"");
        if (code == 0 && !string.IsNullOrWhiteSpace(outText))
        {
            var lines = outText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                var clean = l.Trim().TrimStart('*').Trim();
                if (!string.IsNullOrEmpty(clean)) branches.Add(clean);
            }
        }

        return branches;
    }

    public async Task<(bool Success, string Output)> MergeBranchAsync(string repoDir, string branchName)
    {
        if (!await IsGitRepositoryAsync(repoDir))
        {
            return (false, "Not a git repository");
        }

        var safeBranch = SanitizeBranchName(branchName);
        var (code, outText, errText) = await RunGitCommandAsync(repoDir, $"merge --no-ff \"{safeBranch}\" -m \"Merge swarm branch '{safeBranch}'\"");

        bool success = code == 0;
        string combined = string.IsNullOrWhiteSpace(errText) ? outText : $"{outText}\n{errText}".Trim();
        Logger.Info($"[GitWorktree] Merged branch '{safeBranch}' -> Code: {code}");

        return (success, combined);
    }

    public async Task<int> PruneStaleWorktreesAsync(string repoDir)
    {
        if (!await IsGitRepositoryAsync(repoDir)) return 0;

        var (code, outText, _) = await RunGitCommandAsync(repoDir, "worktree prune -v");
        if (code == 0 && !string.IsNullOrWhiteSpace(outText))
        {
            var lines = outText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length;
        }

        return 0;
    }

    public static string SanitizeBranchName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "swarm/unnamed";
        var invalid = new[] { ' ', '~', '^', ':', '?', '*', '[', '\\' };
        var clean = string.Join("-", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return clean.Trim('/');
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunGitCommandAsync(string workingDir, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ResolveGitBinary(),
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            psi.Environment["TERM"] = "dumb";
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            proc.StandardInput.Close();


            var outTask = proc.StandardOutput.ReadToEndAsync();
            var errTask = proc.StandardError.ReadToEndAsync();

            var completed = await Task.WhenAny(Task.WhenAll(outTask, errTask), Task.Delay(15000));
            if (completed == Task.Delay(15000))
            {
                try { proc.Kill(); } catch { }
                return (-1, string.Empty, "Git command timed out after 15 seconds");
            }

            await proc.WaitForExitAsync();
            return (proc.ExitCode, await outTask, await errTask);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[GitWorktree] Execution failed for 'git {arguments}': {ex.Message}");
            return (-1, string.Empty, ex.Message);
        }
    }

    private static string ResolveGitBinary()
    {
        if (OperatingSystem.IsWindows())
        {
            var candidates = new[]
            {
                @"C:\Program Files\Git\cmd\git.exe",
                @"C:\Program Files\Git\bin\git.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\cmd\git.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Git\cmd\git.exe")
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
        }
        return "git";
    }
}

