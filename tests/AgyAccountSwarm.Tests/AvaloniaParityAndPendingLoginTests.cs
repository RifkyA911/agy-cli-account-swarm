using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using AgyAccountSwarm.ViewModels;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class AvaloniaParityAndPendingLoginTests
{
    private class FakeTerminalLauncher : ITerminalLauncherService
    {
        public bool LastForceLoginPrompt { get; private set; }
        public AccountProfile? LastProfile { get; private set; }
        public TerminalType LastTerminal { get; private set; }

        public string? FindAgyExecutablePath() => "agy.exe";
        public bool IsWindowsTerminalAvailable() => true;

        public Task<Process?> LaunchProfileAsync(AccountProfile profile, TerminalType preferredTerminal, bool forceLoginPrompt = false, string? sessionArgs = null)
        {
            LastProfile = profile;
            LastTerminal = preferredTerminal;
            LastForceLoginPrompt = forceLoginPrompt;
            return Task.FromResult<Process?>(null);
        }

        public Task<Process?> LaunchHeadlessAgyAsync(
            AccountProfile profile,
            string workingDir,
            string prompt,
            Action<string>? onOutputLine = null,
            Action<string>? onErrorLine = null,
            bool dangerouslySkipPermissions = true)
        {
            return Task.FromResult<Process?>(null);
        }

        public Task<List<Process>> LaunchSwarmAsync(IEnumerable<AccountProfile> profiles, TerminalType terminal, SwarmLaunchMode swarmMode)
        {
            return Task.FromResult(new List<Process>());
        }

        public string GetCliSnippet(AccountProfile profile, TerminalType terminal) => "agy";
        public void OpenProfileFolder(AccountProfile profile) { }
        public void OpenWorkspaceFolder(AccountProfile profile) { }
    }

    private class FakeAuthDetector : IAuthDetectorService
    {
        public ProfileAuthStatus StatusToReturn { get; set; } = new ProfileAuthStatus { Status = AuthStatusType.NeedsLogin };

        public Task<ProfileAuthStatus> DetectAuthStatusAsync(AccountProfile profile, bool allowCliSpawn = true)
        {
            return Task.FromResult(StatusToReturn);
        }

        public List<ConversationSessionItem> GetAvailableSessions(AccountProfile profile)
        {
            return new List<ConversationSessionItem>();
        }
    }

    [Fact]
    public void UnauthenticatedAccount_BlocksToggleDetails_AndNotifiesUser()
    {
        var profile = new AccountProfile
        {
            Name = "PendingProfile",
            AuthStatus = new ProfileAuthStatus { Status = AuthStatusType.NeedsLogin }
        };

        var launcher = new FakeTerminalLauncher();
        var authDetector = new FakeAuthDetector();
        var audio = new AudioService { IsEnabled = false };

        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio);

        Assert.True(vm.NeedsLogin);
        Assert.False(vm.IsAuthenticated);
        Assert.Contains("Pending Login", vm.DetailsButtonTooltip);

        string? notification = null;
        vm.OnNotificationRequested += msg => notification = msg;

        // Attempting to expand details when unauthenticated
        vm.ToggleDetailsCommand.Execute(null);

        Assert.False(vm.IsDetailsExpanded);
        Assert.NotNull(notification);
        Assert.Contains("not authenticated", notification);
    }

    [Fact]
    public void AuthenticatedAccount_AllowsToggleDetails()
    {
        var profile = new AccountProfile
        {
            Name = "ActiveProfile",
            AuthStatus = new ProfileAuthStatus { Status = AuthStatusType.Authenticated, AccountEmail = "active@gmail.com" }
        };

        var launcher = new FakeTerminalLauncher();
        var authDetector = new FakeAuthDetector();
        var audio = new AudioService { IsEnabled = false };

        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio);

        Assert.False(vm.NeedsLogin);
        Assert.True(vm.IsAuthenticated);

        // Toggle open
        vm.ToggleDetailsCommand.Execute(null);
        Assert.True(vm.IsDetailsExpanded);

        // Toggle closed
        vm.ToggleDetailsCommand.Execute(null);
        Assert.False(vm.IsDetailsExpanded);
    }

    [Fact]
    public async Task RefreshAuthStatus_WhenUnauthenticated_CollapsesDetailsAndDeselectsFromSwarm()
    {
        var profile = new AccountProfile
        {
            Name = "FlakyProfile",
            IsSelectedForSwarm = true,
            AuthStatus = new ProfileAuthStatus { Status = AuthStatusType.Authenticated }
        };

        var launcher = new FakeTerminalLauncher();
        var authDetector = new FakeAuthDetector
        {
            StatusToReturn = new ProfileAuthStatus { Status = AuthStatusType.NeedsLogin }
        };
        var audio = new AudioService { IsEnabled = false };

        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio);
        vm.IsDetailsExpanded = true;
        vm.IsSelectedForSwarm = true;

        await vm.RefreshAuthStatusAsync(allowCliSpawn: false);

        Assert.False(vm.IsAuthenticated);
        Assert.True(vm.NeedsLogin);
        Assert.False(vm.IsDetailsExpanded);
        Assert.False(vm.IsSelectedForSwarm);
    }

    [Fact]
    public async Task AuthenticateCommand_LaunchesWithForceLoginPromptTrue()
    {
        var profile = new AccountProfile
        {
            Name = "LoginProfile",
            AuthStatus = new ProfileAuthStatus { Status = AuthStatusType.NeedsLogin }
        };

        var launcher = new FakeTerminalLauncher();
        var authDetector = new FakeAuthDetector();
        var audio = new AudioService { IsEnabled = false };

        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio);

        await vm.AuthenticateAsync(TerminalType.WindowsTerminal);

        Assert.True(launcher.LastForceLoginPrompt);
        Assert.Equal(profile, launcher.LastProfile);
        Assert.Equal(TerminalType.WindowsTerminal, launcher.LastTerminal);
    }

    [Fact]
    public void GenerateExecutiveReportHtml_HasHighContrastWhitePrintLayout()
    {
        var launcher = new FakeTerminalLauncher();
        var authDetector = new FakeAuthDetector();
        var audio = new AudioService { IsEnabled = false };
        var storage = new ProfileStorageService();
        var localization = new LocalizationService();
        var mcp = new McpService();
        var telemetry = new TelemetryService();

        var mainVm = new MainViewModel(storage, launcher, authDetector, audio, localization, mcp, telemetry);

        var html = mainVm.GenerateExecutiveReportHtml();

        Assert.NotNull(html);
        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("background: #ffffff !important;", html);
        Assert.Contains("color: #0f172a !important;", html);
        Assert.Contains("th { background: #f1f5f9 !important; color: #334155 !important;", html);
        Assert.Contains("td { border: 1px solid #e2e8f0 !important; color: #1e293b !important;", html);
        Assert.Contains("Executive Telemetry Report", html);
        Assert.Contains("Swarm Fleet Intelligence", html);

        // Ensure no white-on-white collisions
        Assert.DoesNotContain("color: #f8fafc", html);
    }

    [Fact]
    public void AvaloniaAxaml_FluffyPurrRemoved_AndOutlineButtonsApplied()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "AgyAccountSwarm.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        var avaloniaAxamlPath = Path.Combine(dir, "AgyAccountSwarm.Avalonia", "MainWindow.axaml");
        Assert.True(File.Exists(avaloniaAxamlPath));

        var content = File.ReadAllText(avaloniaAxamlPath);

        // Verify Fluffy Purr is completely removed
        Assert.DoesNotContain("Fluffy Purr", content);
        Assert.DoesNotContain("ReplayWelcomeCommand", content);

        // Verify Documentation buttons have outline class
        Assert.Contains("CommandParameter=\"Architecture\" Classes=\"outline\"", content);
        Assert.Contains("CommandParameter=\"ProfileDoctor\" Classes=\"outline\"", content);
        Assert.Contains("CommandParameter=\"SwarmWorkflow\" Classes=\"outline\"", content);
        Assert.Contains("CommandParameter=\"Troubleshooting\" Classes=\"outline\"", content);

        // Verify Swarm Worker utility buttons have outline class
        Assert.Contains("Command=\"{Binding PruneWorktreesCommand}\"\n                                            Classes=\"outline\"", content.Replace("\r\n", "\n"));
        Assert.Contains("Command=\"{Binding OpenProjectBlackboardCommand}\" Classes=\"outline\"", content);
        Assert.Contains("Command=\"{Binding SetProjectTabCommand}\" CommandParameter=\"2\" Classes=\"outline\"", content);
    }

    [Fact]
    public void AvaloniaAxaml_WindowDimensionsMatchWpf_AndDataTemplatesUseViewModelType()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "AgyAccountSwarm.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        var avaloniaAxamlPath = Path.Combine(dir, "AgyAccountSwarm.Avalonia", "MainWindow.axaml");
        Assert.True(File.Exists(avaloniaAxamlPath));

        var content = File.ReadAllText(avaloniaAxamlPath);

        // Verify exact dimensions match WPF (1240x760, min 1050x640)
        Assert.Contains("Width=\"1240\" Height=\"760\"", content);
        Assert.Contains("MinWidth=\"1050\" MinHeight=\"640\"", content);

        // Verify DataTemplate uses ViewModel type, not models:AccountProfile
        Assert.DoesNotContain("DataTemplate DataType=\"models:AccountProfile\"", content);
        Assert.Contains("DataTemplate DataType=\"vm:AvaloniaProfileItemViewModel\"", content);

        // Verify outline button for Sync Swarm in navbar
        Assert.Contains("Command=\"{Binding SyncSwarmCommand}\"", content);
        Assert.Contains("Classes=\"outline\"", content);
    }

    [Fact]
    public void ProfileItemViewModel_IsDefaultPrimary_Parity()
    {
        var defaultProfile = new AccountProfile { Id = "main", Name = "Default (Main Account)", CustomProfilePath = "" };
        var vmDefault = new ProfileItemViewModel(
            defaultProfile,
            new FakeTerminalLauncher(),
            new FakeAuthDetector(),
            new AudioService { IsEnabled = false });

        Assert.True(vmDefault.IsDefaultProfile);
        Assert.True(vmDefault.IsDefaultPrimary);
        Assert.False(vmDefault.CanDelete);

        var workerProfile = new AccountProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Worker-Alpha",
            CustomProfilePath = @"C:\Users\test\.gemini_worker"
        };
        var vmWorker = new ProfileItemViewModel(
            workerProfile,
            new FakeTerminalLauncher(),
            new FakeAuthDetector(),
            new AudioService { IsEnabled = false });

        Assert.False(vmWorker.IsDefaultProfile);
        Assert.False(vmWorker.IsDefaultPrimary);
        Assert.True(vmWorker.CanDelete);
    }

    [Fact]
    public void AvaloniaAppAxaml_ButtonOutline_UsesTransparentBackground()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "AgyAccountSwarm.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        var appAxamlPath = Path.Combine(dir, "AgyAccountSwarm.Avalonia", "App.axaml");
        Assert.True(File.Exists(appAxamlPath));

        var content = File.ReadAllText(appAxamlPath);
        Assert.Contains("<Style Selector=\"Button.outline, Button.modern\">", content);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", content);
    }

    [Fact]
    public void AuthDetectorService_UpgradeGoogleAvatarResolution_UpgradesToCrispRetina()
    {
        var rawUrl = "https://lh3.googleusercontent.com/a/ACg8ocL81xyz=s96-c";
        var upgraded = AgyAccountSwarm.Services.AuthDetectorService.UpgradeGoogleAvatarResolution(rawUrl);
        Assert.Equal("https://lh3.googleusercontent.com/a/ACg8ocL81xyz=s384-c", upgraded);

        var queryUrl = "https://lh3.googleusercontent.com/a/ACg8ocL81xyz=s64?authuser=0";
        var upgradedQuery = AgyAccountSwarm.Services.AuthDetectorService.UpgradeGoogleAvatarResolution(queryUrl);
        Assert.Contains("=s384-c", upgradedQuery);
    }

    [Fact]
    public void DispatchedWorkerTask_CanStop_And_IsActive_Lifecycle()
    {
        var task = new AgyAccountSwarm.Models.DispatchedWorkerTask
        {
            Status = "Running"
        };
        Assert.True(task.IsActive);
        Assert.True(task.CanStop);

        task.Status = "Completed";
        Assert.False(task.IsActive);
        Assert.False(task.CanStop);

        task.Status = "Quota Exceeded";
        Assert.False(task.IsActive);
        Assert.False(task.CanStop);

        task.Status = "Stopped";
        Assert.False(task.IsActive);
        Assert.False(task.CanStop);
    }

    [Fact]
    public void AvaloniaAxaml_ExecutiveReports_HasDedicatedActionRow_AndFleetEngineWrap()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "AgyAccountSwarm.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        var avaloniaAxamlPath = Path.Combine(dir, "AgyAccountSwarm.Avalonia", "MainWindow.axaml");
        Assert.True(File.Exists(avaloniaAxamlPath));

        var content = File.ReadAllText(avaloniaAxamlPath);

        // Verify Executive Telemetry Reports has dedicated Row 2
        Assert.Contains("<!-- Row 2: Dedicated Action Button Row -->", content);
        Assert.Contains("Command=\"{Binding ExportPdfReportCommand}\"", content);
        Assert.Contains("Command=\"{Binding ExportLogsExcelCommand}\"", content);

        // Verify Connected Accounts in Active Fleet Engine uses WrapPanel
        Assert.Contains("<!-- Assigned Swarm Accounts with WrapPanel protection -->", content);

        // Verify Abort Fleet and Stop Worker have IsEnabled bindings
        Assert.Contains("IsEnabled=\"{Binding CanAbortFleet}\"", content);
        Assert.Contains("IsEnabled=\"{Binding CanStop}\"", content);
    }
}
