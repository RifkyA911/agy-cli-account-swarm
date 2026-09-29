using System;
using System.IO;
using System.Threading.Tasks;
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
        Assert.Contains("set \"SSH_CONNECTION=\"", content);
        Assert.Contains("set \"SSH_CLIENT=\"", content);
        Assert.Contains("goto :cli_only", content);
        Assert.DoesNotContain("if \"%1\"==\"--cli-only\" (", content);
    }

    [Fact]
    public void SanitizeBatchString_WithParentheses_StripsParentheses()
    {
        string input = "Default (Main Account)";
        string output = TerminalLauncherService.SanitizeBatchString(input);
        Assert.DoesNotContain("(", output);
        Assert.DoesNotContain(")", output);
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
    public void EnsureLauncherScript_WithInjectionCharactersInProfileName_SanitizesTitleAndCommands()
    {
        var service = new TerminalLauncherService();
        var tempDir = Path.Combine(Path.GetTempPath(), "agy_sec_test_" + Guid.NewGuid().ToString("N"));
        try
        {
            var profile = new AccountProfile
            {
                Name = "Worker & calc.exe | echo pwned",
                CustomProfilePath = tempDir,
                ExtraArguments = "--dangerously-skip-permissions; whoami"
            };

            var scriptPath = service.EnsureLauncherScript(profile);
            var content = File.ReadAllText(scriptPath);

            // Assert: dangerous characters are stripped from title and extraArgs
            Assert.DoesNotContain("&", content.Split('\n').First(l => l.StartsWith("title ")));
            Assert.DoesNotContain("|", content.Split('\n').First(l => l.StartsWith("title ")));
            Assert.DoesNotContain(";", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void BuildWindowsTerminalArguments_WithSemicolonInTitleOrPath_SanitizesDelimiter()
    {
        string title = "AGY [Worker; new-tab cmd.exe]";
        string workDir = @"D:\Works\Project; evil";
        string scriptPath = @"C:\Profiles\Worker\run-agy.cmd";

        string args = TerminalLauncherService.BuildWindowsTerminalArguments(title, workDir, scriptPath);

        // Windows Terminal semicolon delimiter should be replaced
        Assert.DoesNotContain("Worker;", args);
        Assert.Contains("Worker - new-tab cmd.exe", args);
    }

    [Fact]
    public void SanitizeSessionArgs_OnlyAllowsWhitelistedArguments()
    {
        Assert.Equal("--continue", TerminalLauncherService.SanitizeSessionArgs("--continue"));
        Assert.Equal("--conversation conv-123_abc", TerminalLauncherService.SanitizeSessionArgs("--conversation conv-123_abc"));
        
        // Malicious or arbitrary inputs should be rejected
        Assert.Null(TerminalLauncherService.SanitizeSessionArgs("--conversation conv-123; calc.exe"));
        Assert.Null(TerminalLauncherService.SanitizeSessionArgs("--conversation conv-123 & whoami"));
        Assert.Null(TerminalLauncherService.SanitizeSessionArgs("rm -rf /"));
        Assert.Null(TerminalLauncherService.SanitizeSessionArgs("--dangerously-skip-permissions"));
    }

    [Fact]
    public void AccountProfile_PathTraversal_SanitizesProperly()
    {
        var profile = new AccountProfile
        {
            Name = "../../../Windows/System32"
        };

        var safeName = AccountProfile.SanitizeFolderName(profile.Name);
        Assert.DoesNotContain("..", safeName);
        Assert.DoesNotContain("/", safeName);
        Assert.DoesNotContain("\\", safeName);

        var effDir = profile.GetEffectiveProfileDirectory();
        Assert.DoesNotContain("..", effDir);
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.StartsWith(Path.Combine(userHome, ".gemini-profiles"), effDir);
    }

    [Fact]
    public async Task DetectAuthStatus_WithMockSandbox_DetectsEmailHermetically()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "agy_test_" + Guid.NewGuid().ToString("N"));
        var cliDir = Path.Combine(tempDir, ".gemini", "antigravity-cli");
        Directory.CreateDirectory(cliDir);
        try
        {
            // Valid base64 payload: {"email":"dummy_user@example.com","name":"Test User"}
            string base64Payload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"email\":\"dummy_user@example.com\",\"name\":\"Test User\"}")).TrimEnd('=');
            string json = $"{{\"id_token\":\"eyJhbGciOiJub25lIn0.{base64Payload}.dummy_sig\"}}";
            await File.WriteAllTextAsync(Path.Combine(cliDir, "antigravity-oauth-token"), json);

            var service = new AuthDetectorService();
            var profile = new AccountProfile
            {
                Name = "Mock Sandbox",
                CustomProfilePath = tempDir
            };
            var status = await service.DetectAuthStatusAsync(profile);
            Assert.Equal("dummy_user@example.com", status.AccountEmail);
            Assert.Equal(AuthStatusType.Authenticated, status.Status);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                        break;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(200);
                    }
                }
            }
        }
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
        Assert.True(sessions.Count >= 3);
        Assert.True(sessions[0].IsNewChat);
        Assert.True(sessions[1].IsContinueRecent);
        Assert.True(sessions[2].IsCliOnly);
        Assert.Equal("__cli_only__", sessions[2].Id);
    }

    [Fact]
    public void SanitizeSessionArgs_CliOnly_ReturnsCliOnlyFlag()
    {
        Assert.Equal("--cli-only", TerminalLauncherService.SanitizeSessionArgs("--cli-only"));
        Assert.Equal("--cli-only", TerminalLauncherService.SanitizeSessionArgs("__cli_only__"));
        Assert.Equal("--continue", TerminalLauncherService.SanitizeSessionArgs("--continue"));
        Assert.Equal("--conversation conv-1234", TerminalLauncherService.SanitizeSessionArgs("--conversation conv-1234"));
    }

    [Fact]
    public void IsMainDefaultProfile_ClonesAndCopies_ReturnFalse()
    {
        var defaultProfile = new AccountProfile { Name = "Default (Main Account)" };
        Assert.True(defaultProfile.IsMainDefaultProfile());

        var copiedProfile = new AccountProfile { Name = "Default (Main Account) (Copy)" };
        Assert.False(copiedProfile.IsMainDefaultProfile());

        var copyOfProfile = new AccountProfile { Name = "Copy of Default (Main Account)" };
        Assert.False(copyOfProfile.IsMainDefaultProfile());
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

    [Fact]
    public void DangerouslySkipPermissions_IncludedInLauncherScriptAndSnippet()
    {
        var launcher = new TerminalLauncherService();
        var profile = new AccountProfile
        {
            Name = "Automation Bot",
            DangerouslySkipPermissions = true,
            ExtraArguments = "--model gemini-2.5-flash"
        };

        var scriptPath = launcher.EnsureLauncherScript(profile);
        var scriptContent = File.ReadAllText(scriptPath);

        Assert.Contains("--dangerously-skip-permissions", scriptContent);

        var psSnippet = launcher.GetCliSnippet(profile, TerminalType.PowerShell);
        Assert.Contains("--dangerously-skip-permissions", psSnippet);

        var cmdSnippet = launcher.GetCliSnippet(profile, TerminalType.CommandPrompt);
        Assert.Contains("--dangerously-skip-permissions", cmdSnippet);
    }

    [Fact]
    public async Task TokenRestoration_DpapiEncryptedToken_RestoredToPureUtf8JsonWithoutBom()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "agy_token_restore_test_" + Guid.NewGuid().ToString("N"));
        var cliDir = Path.Combine(tempDir, ".gemini", "antigravity-cli");
        Directory.CreateDirectory(cliDir);

        try
        {
            var authDetector = new AuthDetectorService();
            var dpapi = new DataProtectionService();

            string rawJson = "{\"access_token\":\"ya29.secret\",\"id_token\":\"eyJhbGciOiJSUzI1NiJ9.eyJlbWFpbCI6InRlc3RAY2xvbmUuY29tIn0.sig\"}";
            string protectedToken = dpapi.Protect(rawJson);
            var tokenFile = Path.Combine(cliDir, "antigravity-oauth-token");

            // Write encrypted token as might happen from old buggy version
            File.WriteAllText(tokenFile, protectedToken);

            var profile = new AccountProfile
            {
                Name = "Clone Account",
                CustomProfilePath = tempDir
            };

            // Act: Detect status should detect and immediately restore to pure JSON without BOM
            var status = await authDetector.DetectAuthStatusAsync(profile);

            Assert.Equal("test@clone.com", status.AccountEmail);

            // Read raw bytes on disk
            var bytes = await File.ReadAllBytesAsync(tokenFile);
            Assert.True(bytes.Length > 0);

            // Ensure no UTF-8 BOM (EF BB BF)
            if (bytes.Length >= 3)
            {
                bool hasBom = bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                Assert.False(hasBom, "Token file must NOT contain UTF-8 BOM");
            }

            // Ensure it starts with valid JSON '{'
            var restoredText = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.StartsWith("{", restoredText.TrimStart());
            Assert.DoesNotContain("dpapi::", restoredText);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void SearchableConversationDropdown_FilterAndSelection_BehavesAccurately()
    {
        var profile = new AccountProfile { Name = "Chat Filter Test" };
        var vm = new ProfileItemViewModel(profile, new TerminalLauncherService(), new AuthDetectorService(), new AudioService());

        // Add dummy sessions
        var s1 = new ConversationSessionItem { Id = "conv-1", DisplayText = "Fix login bug in terminal", Snippet = "Fix login bug in terminal" };
        var s2 = new ConversationSessionItem { Id = "conv-2", DisplayText = "Add responsive SVG header", Snippet = "Add responsive SVG header" };
        var s3 = new ConversationSessionItem { Id = "conv-3", DisplayText = "Refactor Avalonia Linux UI", Snippet = "Refactor Avalonia Linux UI" };

        vm.AvailableSessions.Clear();
        vm.AvailableSessions.Add(s1);
        vm.AvailableSessions.Add(s2);
        vm.AvailableSessions.Add(s3);
        vm.ApplySessionFilter();

        Assert.Equal(3, vm.FilteredSessions.Count);
        Assert.True(vm.HasFilteredSessions);
        Assert.False(vm.HasNoMatchingSessions);

        // Filter for "Avalonia"
        vm.SessionSearchText = "Avalonia";
        Assert.Single(vm.FilteredSessions);
        Assert.Equal(s3, vm.FilteredSessions[0]);

        // Select session
        vm.ToggleSessionDropdown();
        Assert.True(vm.IsSessionDropdownOpen);

        vm.SelectSession(s3);
        Assert.Equal(s3, vm.SelectedSession);
        Assert.False(vm.IsSessionDropdownOpen); // Closes dropdown

        // Clear search
        vm.ClearSessionSearch();
        Assert.Equal(string.Empty, vm.SessionSearchText);
        Assert.Equal(3, vm.FilteredSessions.Count);
    }
}

