namespace AgyAccountSwarm.Models;

public enum TerminalType
{
    WindowsTerminal,
    PowerShell,
    CommandPrompt
}

public enum AuthStatusType
{
    NotInitialized,
    NeedsLogin,
    Authenticated,
    Error
}

public class ProfileAuthStatus
{
    public AuthStatusType Status { get; set; } = AuthStatusType.NotInitialized;
    public string? AccountEmail { get; set; }
    public string StatusMessage { get; set; } = "Not initialized";
    public DateTime? TokenModifiedAt { get; set; }
}
