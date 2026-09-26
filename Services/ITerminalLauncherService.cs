using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface ITerminalLauncherService
{
    string? FindAgyExecutablePath();
    bool IsWindowsTerminalAvailable();
    Task<Process?> LaunchProfileAsync(AccountProfile profile, TerminalType terminal, bool forceLoginPrompt = false);
    Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, SwarmLaunchMode swarmMode);
    string GetCliSnippet(AccountProfile profile, TerminalType terminal);
    void OpenProfileFolder(AccountProfile profile);
    void OpenWorkspaceFolder(AccountProfile profile);
}
