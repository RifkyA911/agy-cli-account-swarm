using System;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class TerminalLauncherTests
{
    [Fact]
    public void BuildCmdArguments_PathWithSpaces_WrapsInQuotesWithCall()
    {
        // Arrange: Reproduces user's path with spaces:
        // 'C:\Users\rifky\.gemini-profiles\Worker 2\run-agy.cmd'
        string scriptPath = @"C:\Users\rifky\.gemini-profiles\Worker 2\run-agy.cmd";

        // Act
        string args = TerminalLauncherService.BuildCmdArguments(scriptPath);

        // Assert: Must format as: /k call "path"
        Assert.Equal(@"/k call ""C:\Users\rifky\.gemini-profiles\Worker 2\run-agy.cmd""", args);
        Assert.DoesNotContain(@"""""""", args); // Ensure no double consecutive quotes
    }

    [Fact]
    public void BuildWindowsTerminalArguments_WithSpaces_FormatsCleanly()
    {
        // Arrange
        string title = "AGY Swarm - Worker 2";
        string workingDir = @"D:\Works\My Projects\Workspace 1";
        string scriptPath = @"C:\Users\rifky\.gemini-profiles\Worker 2\run-agy.cmd";

        // Act
        string args = TerminalLauncherService.BuildWindowsTerminalArguments(title, workingDir, scriptPath);

        // Assert
        Assert.StartsWith(@"--title ""AGY Swarm - Worker 2""", args);
        Assert.Contains(@"-d ""D:\Works\My Projects\Workspace 1""", args);
        Assert.Contains(@"cmd.exe /k call ""C:\Users\rifky\.gemini-profiles\Worker 2\run-agy.cmd""", args);
    }

    [Fact]
    public void BuildPowerShellCommand_PathWithSpaces_EscapesSingleQuotes()
    {
        // Arrange
        string title = "AGY Swarm - Primary";
        string effectiveDir = @"C:\Users\rifky\.gemini-profiles\Profile 1";
        string workingDir = @"D:\Works\Code Projects";
        string agyBinary = @"C:\Users\rifky\AppData\Roaming\npm\agy.cmd";
        string? extraArgs = "--model 'gemini-3.8-flash'";

        // Act
        string cmd = TerminalLauncherService.BuildPowerShellCommand(title, effectiveDir, workingDir, agyBinary, extraArgs);

        // Assert
        Assert.Contains("$host.UI.RawUI.WindowTitle = 'AGY Swarm - Primary'", cmd);
        Assert.Contains(@"$env:USERPROFILE = 'C:\Users\rifky\.gemini-profiles\Profile 1'", cmd);
        Assert.Contains(@"Set-Location 'D:\Works\Code Projects'", cmd);
        Assert.Contains(@"& 'C:\Users\rifky\AppData\Roaming\npm\agy.cmd'", cmd);
    }
}
