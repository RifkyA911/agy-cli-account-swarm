using System.Collections.Generic;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface IGitWorktreeService
{
    Task<bool> IsGitRepositoryAsync(string directory);
    Task<string?> GetCurrentBranchAsync(string directory);
    Task<List<GitWorktreeInfo>> ListWorktreesAsync(string repoDir);
    Task<(bool Success, string Output, string WorktreePath)> CreateWorktreeAsync(string repoDir, string worktreePath, string branchName);
    Task<(bool Success, string Output)> RemoveWorktreeAsync(string repoDir, string worktreePath, bool force = false);
    Task<List<string>> ListSwarmBranchesAsync(string repoDir);
    Task<(bool Success, string Output)> MergeBranchAsync(string repoDir, string branchName);
    Task<int> PruneStaleWorktreesAsync(string repoDir);
}
