namespace AgyAccountSwarm.Models;

public class AppSettings
{
    public TerminalType PreferredTerminal { get; set; } = TerminalType.WindowsTerminal;
    public string? CustomAgyExecutablePath { get; set; }
    public bool AutoCheckAuthOnStartup { get; set; } = true;
    public bool LaunchSwarmInSplitPanes { get; set; } = true;
}
