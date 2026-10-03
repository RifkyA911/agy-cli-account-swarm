using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

/// <summary>
/// Service responsible for launching isolated Antigravity CLI terminal sessions,
/// generating sandbox launcher scripts, managing swarm process trees, and resolving terminal hosts.
/// </summary>
public interface ITerminalLauncherService
{
    /// <summary>
    /// Discovers the absolute path to the Antigravity CLI executable (<c>agy</c>)
    /// by checking custom settings, standard install directories, and system PATH.
    /// </summary>
    /// <returns>The resolved executable path, or <c>null</c> if not found.</returns>
    string? FindAgyExecutablePath();

    /// <summary>
    /// Checks whether Windows Terminal (<c>wt.exe</c>) is installed and callable on the current machine.
    /// </summary>
    /// <returns><c>true</c> if Windows Terminal is available; otherwise, <c>false</c>.</returns>
    bool IsWindowsTerminalAvailable();

    /// <summary>
    /// Launches an isolated terminal session for a single account profile.
    /// </summary>
    /// <param name="profile">The account profile whose sandbox should be loaded.</param>
    /// <param name="terminal">The terminal emulator type (Windows Terminal, PowerShell, CMD).</param>
    /// <param name="forceLoginPrompt">If <c>true</c>, launches with interactive authentication flow.</param>
    /// <param name="sessionArgs">Optional CLI arguments, such as conversation resumption or CLI-only mode.</param>
    /// <returns>The spawned <see cref="Process"/> instance, or <c>null</c> if launch failed.</returns>
    Task<Process?> LaunchProfileAsync(AccountProfile profile, TerminalType terminal, bool forceLoginPrompt = false, string? sessionArgs = null);

    /// <summary>
    /// Launches an isolated background Antigravity CLI process in headless/silent mode,
    /// redirecting standard output and error streams for real-time telemetry capture.
    /// </summary>
    Task<Process?> LaunchHeadlessAgyAsync(
        AccountProfile profile,
        string workingDir,
        string prompt,
        System.Action<string>? onOutputLine = null,
        System.Action<string>? onErrorLine = null,
        bool dangerouslySkipPermissions = true);

    /// <summary>
    /// Launches a swarm batch execution across multiple selected account profiles simultaneously
    /// utilizing split-panes, separate tabs, or independent windows.
    /// </summary>
    /// <param name="profiles">The collection of selected account profiles to include in the swarm.</param>
    /// <param name="terminal">The terminal emulator to host the swarm sessions.</param>
    /// <param name="swarmMode">The window layout arrangement (SplitPanes, SeparateTabs, SeparateWindows).</param>
    /// <returns>List of spawned <see cref="Process"/> instances.</returns>
    Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, SwarmLaunchMode swarmMode);

    /// <summary>
    /// Generates a copy-pasteable terminal command snippet configured with the profile's sandbox environment.
    /// </summary>
    /// <param name="profile">The target account profile.</param>
    /// <param name="terminal">The target terminal type.</param>
    /// <returns>A command-line string ready to run in terminal.</returns>
    string GetCliSnippet(AccountProfile profile, TerminalType terminal);

    /// <summary>
    /// Opens the profile's isolated sandbox directory in Windows Explorer.
    /// </summary>
    /// <param name="profile">The account profile to open.</param>
    void OpenProfileFolder(AccountProfile profile);

    /// <summary>
    /// Opens the profile's designated workspace directory in Windows Explorer.
    /// </summary>
    /// <param name="profile">The account profile whose workspace should be opened.</param>
    void OpenWorkspaceFolder(AccountProfile profile);
}
