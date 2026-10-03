using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class FleetDispatcherE2ETests
{
    [Theory]
    [InlineData("Thinking about optimal architecture...", "🧠 Thinking & analyzing...")]
    [InlineData("Analyzing repository structure", "🧠 Thinking & analyzing...")]
    [InlineData("Calling tool run_command with arguments: git status", "⚡ Executing command...")]
    [InlineData("Executing bash script build.sh", "⚡ Executing command...")]
    [InlineData("$ dotnet build", "⚡ Executing command...")]
    [InlineData("> npm test", "⚡ Executing command...")]
    [InlineData("Calling tool view_file for Models/AccountProfile.cs", "📖 Reading file / context...")]
    [InlineData("Reading input manifest file", "📖 Reading file / context...")]
    [InlineData("Calling tool write_to_file for Services/FleetDispatcherService.cs", "✏️ Writing / editing code...")]
    [InlineData("Calling tool replace_file_content in MainWindow.xaml", "✏️ Writing / editing code...")]
    [InlineData("Writing test suite to tests/E2ETests.cs", "✏️ Writing / editing code...")]
    [InlineData("Searching codebase for IClipboard references", "🔍 Searching codebase / tools...")]
    [InlineData("git checkout -b swarm/worker_feature", "🌿 Git operations...")]
    [InlineData("jetski: tool permission required but auto-denied in headless mode", "⚠️ Permission required / denied")]
    [InlineData("Unhandled Exception: NullReferenceException in module", "⚠️ Unhandled Exception: NullReferenceException in module")]
    [InlineData("Turn 2: Processing user request", "🔄 Turn 2: Processing user request")]
    public void ParseActivityFromOutput_CorrectlyClassifiesCliEvents(string outputLine, string expectedActivitySubstring)
    {
        var activity = FleetDispatcherService.ParseActivityFromOutput(outputLine);
        Assert.False(string.IsNullOrWhiteSpace(activity));
        Assert.Contains(expectedActivitySubstring, activity);
    }

    [Fact]
    public void DispatchedWorkerTask_ExecutionModeAndToggleOutput_OperatesCorrectly()
    {
        var task = new DispatchedWorkerTask
        {
            ProfileName = "Alice Pro",
            AssignedRole = "Backend Engineer",
            ExecutionMode = FleetExecutionMode.HeadlessSilent,
            IsOutputExpanded = false
        };

        Assert.Equal("⚡ Silent", task.ExecutionModeText);
        Assert.False(task.IsOutputExpanded);

        task.ToggleOutput();
        Assert.True(task.IsOutputExpanded);

        task.ToggleOutput();
        Assert.False(task.IsOutputExpanded);

        task.ExecutionMode = FleetExecutionMode.VisibleTerminal;
        Assert.Equal("🖥️ Terminal", task.ExecutionModeText);
    }

    [Fact]
    public void DispatchedWorkerTask_ElapsedDurationCalculation_FormatsAccurately()
    {
        var task = new DispatchedWorkerTask
        {
            StartedAt = DateTime.UtcNow.AddMinutes(-2).AddSeconds(-15)
        };

        var duration = DateTime.UtcNow - task.StartedAt.Value;
        task.ElapsedTimeFormatted = $"{duration.Minutes:D2}:{duration.Seconds:D2}";

        Assert.Equal("02:15", task.ElapsedTimeFormatted);
    }

    [Fact]
    public async Task StopTaskAsync_MarksTaskStoppedWithActivitySentinel()
    {
        var dispatcher = new FleetDispatcherService();
        var task = new DispatchedWorkerTask
        {
            ProfileName = "Worker Bravo",
            AssignedRole = "QA Specialist",
            Status = "Running",
            StatusColor = "#10B981",
            CurrentActivity = "Executing test suite..."
        };

        var stopped = await dispatcher.StopTaskAsync(task);

        Assert.True(stopped);
        Assert.Equal("Stopped", task.Status);
        Assert.Equal("#EF4444", task.StatusColor);
        Assert.Equal("🛑 Stopped by user", task.CurrentActivity);
        Assert.NotNull(task.CompletedAt);
    }

    [Fact]
    public async Task AbortFleetAsync_TerminatesAllRunningTasks()
    {
        var dispatcher = new FleetDispatcherService();
        var tasks = new List<DispatchedWorkerTask>
        {
            new() { ProfileName = "W1", Status = "Running", StatusColor = "#10B981" },
            new() { ProfileName = "W2", Status = "Launching", StatusColor = "#3B82F6" },
            new() { ProfileName = "W3", Status = "Completed", StatusColor = "#10B981" }
        };

        int killed = await dispatcher.AbortFleetAsync(tasks);

        Assert.Equal(2, killed);
        Assert.Equal("Stopped", tasks[0].Status);
        Assert.Equal("Stopped", tasks[1].Status);
        Assert.Equal("Completed", tasks[2].Status); // Already completed, not aborted
    }

    [Fact]
    public async Task E2E_DispatchFleetAsync_HeadlessSilent_CapturesLiveTelemetryAndOutput()
    {
        var mockGitService = new MockGitWorktreeService();
        var mockLauncherService = new MockTerminalLauncherService();
        var dispatcher = new FleetDispatcherService(mockGitService, mockLauncherService);

        var workers = new List<AccountProfile>
        {
            new() { Name = "Backend Bob", Description = "Backend API Developer", ColorTag = "#3B82F6" },
            new() { Name = "Frontend Fay", Description = "Frontend UI Architect", ColorTag = "#10B981" }
        };

        var config = new FleetDispatchConfig
        {
            TaskObjective = "Build OAuth2 provider and customer dashboard",
            TargetWorkspace = Directory.GetCurrentDirectory(),
            Mode = DispatchMode.RoleTailored,
            ExecutionMode = FleetExecutionMode.HeadlessSilent,
            DangerouslySkipPermissions = true,
            UseGitWorktrees = true
        };

        var progressReports = new List<FleetProgressReport>();
        var progress = new Progress<FleetProgressReport>(r => progressReports.Add(r));

        var tasks = await dispatcher.DispatchFleetAsync(config, workers, TerminalType.PowerShell, progress);

        Assert.Equal(2, tasks.Count);

        // Verify Worker 1: Backend
        var t1 = tasks[0];
        Assert.Equal("Backend Bob", t1.ProfileName);
        Assert.Equal("Backend Engineer", t1.AssignedRole);
        Assert.Equal(FleetExecutionMode.HeadlessSilent, t1.ExecutionMode);
        Assert.Equal("⚡ Silent", t1.ExecutionModeText);
        Assert.Equal("B", t1.AvatarInitial);
        Assert.Equal("#3B82F6", t1.ColorTag);
        Assert.NotNull(t1.StartedAt);
        Assert.Contains("OAuth2", t1.TailoredPrompt);

        // Verify Worker 2: Frontend
        var t2 = tasks[1];
        Assert.Equal("Frontend Fay", t2.ProfileName);
        Assert.Equal("Frontend Engineer", t2.AssignedRole);
        Assert.Equal(FleetExecutionMode.HeadlessSilent, t2.ExecutionMode);
        Assert.Equal("F", t2.AvatarInitial);
        Assert.Equal("#10B981", t2.ColorTag);

        // Verify Launcher Invocations received permission flag
        Assert.True(mockLauncherService.HeadlessCallsCount == 2);
        Assert.All(mockLauncherService.CapturedPermissionFlags, flag => Assert.True(flag));

        // Simulate CLI streaming telemetry events on Worker 1
        mockLauncherService.SimulateOutput(0, "Analyzing project dependencies");
        Assert.Equal("Analyzing project dependencies", t1.LastOutputLine);
        Assert.Contains("Thinking & analyzing", t1.CurrentActivity);

        mockLauncherService.SimulateOutput(0, "Calling tool write_to_file for Controllers/AuthController.cs");
        Assert.Equal("Calling tool write_to_file for Controllers/AuthController.cs", t1.LastOutputLine);
        Assert.Contains("Writing / editing code", t1.CurrentActivity);
        Assert.Contains("Controllers/AuthController.cs", t1.FullOutputLog);

        // Simulate CLI error line on Worker 2
        mockLauncherService.SimulateError(1, "jetski: permission denied on command execution");
        Assert.Contains("Permission required / denied", t2.CurrentActivity);
    }

    [Fact]
    public async Task E2E_DispatchFleetAsync_VisibleTerminal_SuppliesPermissionsFlagToPrompt()
    {
        var mockGitService = new MockGitWorktreeService();
        var mockLauncherService = new MockTerminalLauncherService();
        var dispatcher = new FleetDispatcherService(mockGitService, mockLauncherService);

        var workers = new List<AccountProfile>
        {
            new() { Name = "Tester Tina", Description = "QA & Test Specialist" }
        };

        var config = new FleetDispatchConfig
        {
            TaskObjective = "Write integration test suite for payment gateway",
            TargetWorkspace = Directory.GetCurrentDirectory(),
            Mode = DispatchMode.Broadcast,
            ExecutionMode = FleetExecutionMode.VisibleTerminal,
            DangerouslySkipPermissions = true,
            UseGitWorktrees = false
        };

        var tasks = await dispatcher.DispatchFleetAsync(config, workers, TerminalType.PowerShell);

        Assert.Single(tasks);
        var t = tasks[0];
        Assert.Equal("Tester Tina", t.ProfileName);
        Assert.Equal("QA & Test Specialist", t.AssignedRole);
        Assert.Equal(FleetExecutionMode.VisibleTerminal, t.ExecutionMode);
        Assert.Equal("🖥️ Terminal", t.ExecutionModeText);

        Assert.Single(mockLauncherService.CapturedSessionArgs);
        var sessionArg = mockLauncherService.CapturedSessionArgs[0];
        Assert.StartsWith("--dangerously-skip-permissions -p \"", sessionArg);
        Assert.Contains("Write integration test suite for payment gateway", sessionArg);
    }

    [Fact]
    public void Win32ProcessHelper_ProcessTreeTraversal_IdentifiesDescendantsAndProtectsShells()
    {
        // Mock hierarchy:
        // PID 1000: wt.exe (root terminal host)
        //   PID 1001: cmd.exe (shell)
        //     PID 1002: conhost.exe (console host)
        //     PID 1003: agy.exe (worker process)
        //       PID 1004: rustc.exe (compiler tool spawned by agy)
        var mockProcesses = new List<ProcessNode>
        {
            new(1000, 1, "wt.exe"),
            new(1001, 1000, "cmd.exe"),
            new(1002, 1001, "conhost.exe"),
            new(1003, 1001, "agy.exe"),
            new(1004, 1003, "rustc.exe"),
            new(2000, 1, "notepad.exe") // Unrelated process
        };

        var descendants = Win32ProcessHelper.GetDescendantProcesses(1000, mockProcesses);
        Assert.Equal(4, descendants.Count);
        Assert.Contains(descendants, d => d.ProcessId == 1001);
        Assert.Contains(descendants, d => d.ProcessId == 1002);
        Assert.Contains(descendants, d => d.ProcessId == 1003);
        Assert.Contains(descendants, d => d.ProcessId == 1004);
        Assert.DoesNotContain(descendants, d => d.ProcessId == 2000);

        // Filter out shell processes (wt, cmd, conhost)
        var toKill = descendants
            .Where(d => !FleetDispatcherService.ShellProcessNames.Contains(d.Name))
            .Select(d => d.ProcessId)
            .ToList();

        // Exactly the worker process (agy.exe) and its compiler tool (rustc.exe) should be targeted
        Assert.Equal(new[] { 1003, 1004 }, toKill);

        // Shell processes (cmd.exe, conhost.exe) are preserved
        Assert.DoesNotContain(1001, toKill);
        Assert.DoesNotContain(1002, toKill);
        Assert.DoesNotContain(1000, toKill);
    }

    [Fact]
    public async Task StopTaskAsync_VisibleTerminal_MarksTaskStoppedSafely()
    {
        var dispatcher = new FleetDispatcherService();
        var task = new DispatchedWorkerTask
        {
            ProfileName = "Terminal Worker",
            ExecutionMode = FleetExecutionMode.VisibleTerminal,
            ProcessId = 999999, // Non-existent PID to test safety without killing current shell
            Status = "Running",
            StatusColor = "#10B981"
        };

        bool stopped = await dispatcher.StopTaskAsync(task);
        Assert.True(stopped);
        Assert.Equal("Stopped", task.Status);
        Assert.Equal("#EF4444", task.StatusColor);
        Assert.Equal("🛑 Stopped by user", task.CurrentActivity);
        Assert.NotNull(task.CompletedAt);
    }

    private class MockGitWorktreeService : IGitWorktreeService
    {
        public Task<bool> IsGitRepositoryAsync(string directory) => Task.FromResult(true);
        public Task<string?> GetCurrentBranchAsync(string directory) => Task.FromResult<string?>("master");
        public Task<List<GitWorktreeInfo>> ListWorktreesAsync(string repoDir) => Task.FromResult(new List<GitWorktreeInfo>());
        public Task<(bool Success, string Output, string WorktreePath)> CreateWorktreeAsync(string repoDir, string worktreePath, string branchName)
        {
            return Task.FromResult((true, "Worktree created", worktreePath));
        }
        public Task<(bool Success, string Output)> RemoveWorktreeAsync(string repoDir, string worktreePath, bool force = false) => Task.FromResult((true, "Removed"));
        public Task<List<string>> ListSwarmBranchesAsync(string repoDir) => Task.FromResult(new List<string>());
        public Task<(bool Success, string Output)> MergeBranchAsync(string repoDir, string branchName) => Task.FromResult((true, "Merged"));
        public Task<int> PruneStaleWorktreesAsync(string repoDir) => Task.FromResult(0);
    }

    private class MockTerminalLauncherService : ITerminalLauncherService
    {
        public int HeadlessCallsCount { get; private set; }
        public List<bool> CapturedPermissionFlags { get; } = new();
        public List<string> CapturedSessionArgs { get; } = new();
        private readonly List<Action<string>?> _outputCallbacks = new();
        private readonly List<Action<string>?> _errorCallbacks = new();

        public void SimulateOutput(int workerIndex, string line)
        {
            if (workerIndex < _outputCallbacks.Count)
                _outputCallbacks[workerIndex]?.Invoke(line);
        }

        public void SimulateError(int workerIndex, string line)
        {
            if (workerIndex < _errorCallbacks.Count)
                _errorCallbacks[workerIndex]?.Invoke(line);
        }

        public Task<Process?> LaunchHeadlessAgyAsync(
            AccountProfile profile,
            string workingDir,
            string prompt,
            Action<string>? onOutputLine = null,
            Action<string>? onErrorLine = null,
            bool dangerouslySkipPermissions = true)
        {
            HeadlessCallsCount++;
            CapturedPermissionFlags.Add(dangerouslySkipPermissions);
            _outputCallbacks.Add(onOutputLine);
            _errorCallbacks.Add(onErrorLine);

            return Task.FromResult<Process?>(null);
        }

        public Task<Process?> LaunchProfileAsync(AccountProfile profile, TerminalType terminal, bool forceLoginPrompt = false, string? sessionArgs = null)
        {
            if (sessionArgs != null)
                CapturedSessionArgs.Add(sessionArgs);

            return Task.FromResult<Process?>(null);
        }

        public string? FindAgyExecutablePath() => "agy";
        public bool IsWindowsTerminalAvailable() => true;
        public Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, SwarmLaunchMode swarmMode) => Task.FromResult(new List<Process>());
        public string GetCliSnippet(AccountProfile profile, TerminalType terminal) => "agy";
        public void OpenProfileFolder(AccountProfile profile) { }
        public void OpenWorkspaceFolder(AccountProfile profile) { }
    }
}
