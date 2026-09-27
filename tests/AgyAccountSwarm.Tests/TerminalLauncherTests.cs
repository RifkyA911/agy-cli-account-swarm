using System;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using AgyAccountSwarm.ViewModels;
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

    [Fact]
    public void BuildPowerShellCommand_WhenIsolated_InjectsSshConnectionAndClient()
    {
        // Arrange
        string title = "AGY Swarm - Worker 2";
        string effectiveDir = @"C:\Users\rifky\.gemini-profiles\Worker 2";
        string workingDir = @"C:\Users\rifky\.gemini-profiles\Worker 2\workspace";
        string agyBinary = "agy";

        // Act
        string cmd = TerminalLauncherService.BuildPowerShellCommand(title, effectiveDir, workingDir, agyBinary, null, isIsolated: true);

        // Assert
        Assert.Contains("$env:SSH_CONNECTION = '1'", cmd);
        Assert.Contains("$env:SSH_CLIENT = '1'", cmd);
        Assert.Contains(@"$env:ANTIGRAVITY_APP_DATA_DIR = 'C:\Users\rifky\.gemini-profiles\Worker 2\.gemini\antigravity-cli'", cmd);
    }

    [Fact]
    public void EnsureLauncherScript_WhenIsolated_SetsSshConnectionAndClient()
    {
        // Arrange
        var service = new TerminalLauncherService();
        var tempProfileDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agy_test_worker_" + Guid.NewGuid().ToString("N"));
        try
        {
            var profile = new AccountProfile
            {
                Name = "Worker Test",
                CustomProfilePath = tempProfileDir
            };

            // Act
            var scriptPath = service.EnsureLauncherScript(profile);
            var content = System.IO.File.ReadAllText(scriptPath);

            // Assert
            Assert.Contains("set \"SSH_CONNECTION=1\"", content);
            Assert.Contains("set \"SSH_CLIENT=1\"", content);
            Assert.Contains("set \"ANTIGRAVITY_APP_DATA_DIR=", content);
            Assert.Contains(@"\workspace", content);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempProfileDir))
            {
                try { System.IO.Directory.Delete(tempProfileDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void EnsureLauncherScript_WhenMainProfile_DoesNotSetSshConnection()
    {
        // Arrange
        var service = new TerminalLauncherService();
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profile = new AccountProfile
        {
            Name = "Default (Main Account)",
            CustomProfilePath = userHome
        };

        // Assert IsMainDefaultProfile is true
        Assert.True(profile.IsMainDefaultProfile());

        var scriptPath = service.EnsureLauncherScript(profile);
        var content = System.IO.File.ReadAllText(scriptPath);

        // Assert: Main profile should not disable system keyring fallback
        Assert.DoesNotContain("set \"SSH_CONNECTION=1\"", content);
        Assert.DoesNotContain("set \"SSH_CLIENT=1\"", content);
    }

    [Fact]
    public void GetValidWorkingDirectory_WhenIsolatedAndNoDefaultWorkspace_UsesProfileWorkspaceFolder()
    {
        // Arrange
        var tempProfileDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agy_test_ws_" + Guid.NewGuid().ToString("N"));
        try
        {
            var profile = new AccountProfile
            {
                Name = "Worker WS",
                CustomProfilePath = tempProfileDir
            };

            // Act
            var workDir = TerminalLauncherService.GetValidWorkingDirectory(profile);

            // Assert
            Assert.Equal(System.IO.Path.Combine(tempProfileDir, "workspace"), workDir);
            Assert.True(System.IO.Directory.Exists(workDir));
        }
        finally
        {
            if (System.IO.Directory.Exists(tempProfileDir))
            {
                try { System.IO.Directory.Delete(tempProfileDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void GetValidWorkingDirectory_WhenMainProfile_UsesUserHome()
    {
        // Arrange
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profile = new AccountProfile
        {
            Name = "Default",
            CustomProfilePath = userHome
        };

        // Act
        var workDir = TerminalLauncherService.GetValidWorkingDirectory(profile);

        // Assert
        Assert.Equal(userHome, workDir);
    }

    [Fact]
    public void ExtractEmailFromTokenJson_ValidIdToken_ReturnsEmail()
    {
        string tokenJson = "{\"id_token\":\"eyJhbGciOiJSUzI1NiJ9.eyJlbWFpbCI6Im1pc3NmYXJ1emFuQGdtYWlsLmNvbSJ9.abc\"}";
        var email = AuthDetectorService.ExtractEmailFromTokenJson(tokenJson);
        Assert.Equal("missfaruzan@gmail.com", email);
    }

    [Fact]
    public void ReadWindowsKeyring_Inspect()
    {
        var raw = AuthDetectorService.ReadWindowsCredential("gemini:antigravity");
        if (raw != null)
        {
            var email = AuthDetectorService.ExtractEmailFromTokenJson(raw);
            Assert.True(email == "rifkyakhmad911@gmail.com" || email == "missfaruzan@gmail.com");
        }
    }

    [Fact]
    public async Task DetectAuthStatus_WorkerSpace_DetectsEmail()
    {
        var service = new AuthDetectorService();
        var profile = new AccountProfile
        {
            Name = "Worker Space",
            CustomProfilePath = @"C:\Users\rifky\.gemini-profiles\Worker Space"
        };
        var status = await service.DetectAuthStatusAsync(profile);
        Assert.Equal("missfaruzan@gmail.com", status.AccountEmail);
        Assert.Equal(AuthStatusType.Authenticated, status.Status);
    }

    [Fact]
    public async Task DetectAuthStatus_DefaultProfile_DetectsEmail()
    {
        var service = new AuthDetectorService();
        var profile = new AccountProfile
        {
            Name = "Default",
            CustomProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        var status = await service.DetectAuthStatusAsync(profile);
        Assert.NotNull(status.AccountEmail);
        Assert.Equal(AuthStatusType.Authenticated, status.Status);
    }

    [Fact]
    public void ProfileItemViewModel_DuplicateCommand_TriggersEvent()
    {
        var profile = new AccountProfile { Name = "Test Profile" };
        var vm = new ProfileItemViewModel(profile, new TerminalLauncherService(), new AuthDetectorService(), new AudioService());
        bool triggered = false;
        vm.OnDuplicateRequested += item => { triggered = true; };
        vm.RequestDuplicateCommand.Execute(null);
        Assert.True(triggered);
    }

    [Fact]
    public void ExtractUserInfoFromTokenJson_ValidIdToken_ReturnsPictureAndDisplayName()
    {
        // Sample id_token JWT with email, name, and picture claims
        // Payload: {"email":"user@gmail.com","name":"Google User","picture":"https://lh3.googleusercontent.com/a/sample"}
        string base64Payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"email\":\"user@gmail.com\",\"name\":\"Google User\",\"picture\":\"https://lh3.googleusercontent.com/a/sample\"}")).TrimEnd('=');
        string tokenJson = $"{{\"id_token\":\"header.{base64Payload}.sig\"}}";

        var info = AuthDetectorService.ExtractUserInfoFromTokenJson(tokenJson);

        Assert.Equal("user@gmail.com", info.Email);
        Assert.Equal("Google User", info.DisplayName);
        Assert.Equal("https://lh3.googleusercontent.com/a/sample", info.PictureUrl);
    }

    [Fact]
    public void GetAvailableSessions_ReturnsNewChatAndContinueRecent()
    {
        var service = new AuthDetectorService();
        var profile = new AccountProfile { Name = "Temp Profile" };
        var sessions = service.GetAvailableSessions(profile);

        Assert.NotNull(sessions);
        Assert.True(sessions.Count >= 2);
        Assert.True(sessions[0].IsNewChat);
        Assert.True(sessions[1].IsContinueRecent);
    }

    [Fact]
    public void ProfileItemViewModel_ContextMetricsAndInspection_AreAccurate()
    {
        var profile = new AccountProfile { Name = "Context Test", PreferredModel = "gemini-1.5-pro" };
        var vm = new ProfileItemViewModel(profile, new TerminalLauncherService(), new AuthDetectorService(), new AudioService());

        // Context metrics initialized
        Assert.True(vm.ModelContextLimit >= 1000000L);

        // Toggle /usage inspection
        vm.ToggleUsageInspectionCommand.Execute(null);
        Assert.True(vm.IsInspectorOpen);
        Assert.Equal("usage", vm.InspectorMode);

        // Toggle /context inspection
        vm.ToggleContextInspectionCommand.Execute(null);
        Assert.True(vm.IsInspectorOpen);
        Assert.Equal("context", vm.InspectorMode);

        // Close inspection
        vm.CloseInspectorCommand.Execute(null);
        Assert.False(vm.IsInspectorOpen);
    }
}
