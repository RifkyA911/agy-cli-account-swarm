namespace AgyAccountSwarm.Models;

public class AppSettings
{
    public TerminalType PreferredTerminal { get; set; } = TerminalType.WindowsTerminal;
    public SwarmLaunchMode SwarmMode { get; set; } = SwarmLaunchMode.SplitPanes;
    public string? CustomAgyExecutablePath { get; set; }
    public bool AutoCheckAuthOnStartup { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public string Language { get; set; } = "en";
    public string PreferredChartMode { get; set; } = "Bar";
}
