using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class FleetDispatcherTests
{
    [Fact]
    public void PromptSynthesis_Broadcast_ReturnsExactBaseObjective()
    {
        var dispatcher = new FleetDispatcherService();
        var prompt = dispatcher.SynthesizePrompt("Fix race condition in SQLite writer", "Backend", DispatchMode.Broadcast, 0, 3);
        
        Assert.Equal("Fix race condition in SQLite writer", prompt);
    }

    [Theory]
    [InlineData(0, "Approach A")]
    [InlineData(1, "Approach B")]
    [InlineData(2, "Reviewer")]
    public void PromptSynthesis_Consensus_AssignsSpecificRolesByIndex(int index, string expectedRoleKeyword)
    {
        var dispatcher = new FleetDispatcherService();
        var prompt = dispatcher.SynthesizePrompt("Refactor auth pipeline", "Worker", DispatchMode.Consensus, index, 3);

        Assert.Contains(expectedRoleKeyword, prompt);
        Assert.Contains("Refactor auth pipeline", prompt);
    }

    [Fact]
    public void PromptSynthesis_RoleTailored_InjectsRolePrompt()
    {
        var dispatcher = new FleetDispatcherService();
        var prompt = dispatcher.SynthesizePrompt("Build billing checkout", "Security Auditor", DispatchMode.RoleTailored, 0, 1);

        Assert.Contains("[ROLE: SECURITY AUDITOR]", prompt);
        Assert.Contains("Build billing checkout", prompt);
        Assert.Contains("Security Auditor", prompt);
    }

    [Fact]
    public void PromptSynthesis_EmptyObjective_ReturnsEmptyString()
    {
        var dispatcher = new FleetDispatcherService();
        var prompt = dispatcher.SynthesizePrompt("", "Architect", DispatchMode.Broadcast, 0, 1);

        Assert.Equal(string.Empty, prompt);
    }

    [Theory]
    [InlineData("feature/login page test", "feature/login-page-test")]
    [InlineData("fix:bug~v1^2*3", "fix-bug-v1-2-3")]
    [InlineData("/swarm/task-1/", "swarm/task-1")]
    [InlineData("", "swarm/unnamed")]
    public void SanitizeBranchName_CleansInvalidGitCharacters(string input, string expected)
    {
        var sanitized = GitWorktreeService.SanitizeBranchName(input);
        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void ParseWorktreePorcelain_ParsesValidMultiWorktreeOutput()
    {
        var samplePorcelain = 
@"worktree /repos/main
HEAD 4f2c9e78a1234567890abcdef1234567890abcdef
branch refs/heads/main

worktree /repos/swarm/worker_01
HEAD 1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b
branch refs/heads/swarm/worker_01_feature

worktree /repos/swarm/locked_worktree
HEAD 9f8e7d6c5b4a3f2e1d0c9b8a7f6e5d4c3b2a1f0e
branch refs/heads/swarm/locked_branch
locked Build in progress
";

        var worktrees = GitWorktreeService.ParseWorktreePorcelain(samplePorcelain);

        Assert.Equal(3, worktrees.Count);
        
        Assert.Equal("/repos/main", worktrees[0].Path);
        Assert.Equal("main", worktrees[0].Branch);
        Assert.False(worktrees[0].IsLocked);

        Assert.Equal("/repos/swarm/worker_01", worktrees[1].Path);
        Assert.Equal("swarm/worker_01_feature", worktrees[1].Branch);
        Assert.False(worktrees[1].IsLocked);

        Assert.Equal("/repos/swarm/locked_worktree", worktrees[2].Path);
        Assert.Equal("swarm/locked_branch", worktrees[2].Branch);
        Assert.True(worktrees[2].IsLocked);
        Assert.Equal("Build in progress", worktrees[2].LockReason);
    }

    [Fact]
    public void ResourceCheckResult_Defaults_AreSafe()
    {
        var res = new ResourceCheckResult();
        Assert.True(res.IsSafe);
        Assert.Null(res.ErrorMessage);
        Assert.Null(res.WarningMessage);
    }

    [Fact]
    public void FleetDispatchConfig_Defaults_InitializedProperly()
    {
        var config = new FleetDispatchConfig();
        Assert.True(config.UseGitWorktrees);
        Assert.Equal(DispatchMode.RoleTailored, config.Mode);
        Assert.NotNull(config.SelectedWorkerNames);
        Assert.Empty(config.SelectedWorkerNames);
        Assert.Empty(config.TaskObjective);
    }


    [Fact]
    public async Task CheckPreflightResourcesAsync_WithCurrentDirectory_ReturnsValidResourceMetrics()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dispatcher = new FleetDispatcherService();

        var result = await dispatcher.CheckPreflightResourcesAsync(currentDir);

        Assert.NotNull(result);
        Assert.True(result.AvailableDiskGb > 0);
        Assert.True(result.AvailableMemoryGb > 0);
        // Current directory is a git repo
        Assert.True(result.IsGitRepository);
        Assert.False(string.IsNullOrWhiteSpace(result.CurrentBranch));
    }
}
