using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using AgyAccountSwarm.ViewModels;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class ProfileDoctorAndFleetTests
{
    [Fact]
    public async Task ProfileDoctor_CleanStuckLocks_RemovesLockFilesSafely()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "agy_doctor_test_" + Guid.NewGuid().ToString("N"));
        var cliDir = Path.Combine(tempDir, ".gemini", "antigravity-cli");
        var presenceDir = Path.Combine(cliDir, "presence");
        Directory.CreateDirectory(presenceDir);

        var lock1 = Path.Combine(cliDir, "session.lock");
        var lock2 = Path.Combine(presenceDir, "active.lock");
        var regularFile = Path.Combine(cliDir, "config.json");

        await File.WriteAllTextAsync(lock1, "locked");
        await File.WriteAllTextAsync(lock2, "locked");
        await File.WriteAllTextAsync(regularFile, "{}");

        var profile = new AccountProfile
        {
            Name = "DoctorTestProfile",
            CustomProfilePath = tempDir
        };

        var doctor = new ProfileDoctorService();

        try
        {
            // Act
            int cleaned = await doctor.CleanStuckLocksAsync(profile);

            // Assert
            Assert.Equal(2, cleaned);
            Assert.False(File.Exists(lock1));
            Assert.False(File.Exists(lock2));
            Assert.True(File.Exists(regularFile));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task ProfileDoctor_AddWorkspaceToTrusted_RegistersPathSafely()
    {
        // Arrange
        var tempProfileDir = Path.Combine(Path.GetTempPath(), "agy_workspace_test_" + Guid.NewGuid().ToString("N"));
        var antigravityDir = Path.Combine(tempProfileDir, ".gemini", "antigravity-cli");
        Directory.CreateDirectory(antigravityDir);

        var testWsDir = Path.Combine(Path.GetTempPath(), "agy_workspace_folder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testWsDir);

        var initialSettings = new
        {
            theme = "dark",
            trustedWorkspaces = new[] { "D:\\Initial\\Workspace" }
        };
        var settingsPath = Path.Combine(antigravityDir, "settings.json");
        await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(initialSettings));

        var profile = new AccountProfile
        {
            Name = "WorkspaceProfile",
            CustomProfilePath = tempProfileDir,
            DefaultWorkspace = testWsDir
        };

        var doctor = new ProfileDoctorService();

        try
        {
            // Act
            bool result = await doctor.AddWorkspaceToTrustedAsync(profile);

            // Assert
            Assert.True(result);
            string jsonText = await File.ReadAllTextAsync(settingsPath);
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            var workspaces = root.GetProperty("trustedWorkspaces").EnumerateArray().Select(e => e.GetString()).ToList();

            Assert.Contains("D:\\Initial\\Workspace", workspaces);
            Assert.Contains(Path.GetFullPath(testWsDir).TrimEnd('\\', '/'), workspaces);
        }
        finally
        {
            if (Directory.Exists(tempProfileDir)) Directory.Delete(tempProfileDir, true);
            if (Directory.Exists(testWsDir)) Directory.Delete(testWsDir, true);
        }
    }

    [Fact]
    public void ProfileItemViewModel_BurnRateAndExhaustionForecast_CalculatedAccurately()
    {
        // Arrange
        var profile = new AccountProfile
        {
            Name = "BurnRateProfile",
            Tier = "Basic",
            AuthStatus = new ProfileAuthStatus
            {
                DailyQuotaLimit = 100,
                TodayTurnsCount = 60 // 40 remaining (40%)
            }
        };

        var launcher = new TerminalLauncherService();
        var authDetector = new AuthDetectorService();
        var audio = new AudioService { IsEnabled = false };
        var doctor = new ProfileDoctorService();

        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio, doctor);

        // Act & Assert
        Assert.Equal(100, vm.DailyQuotaLimit);
        Assert.Equal(60, vm.TodayTurnsCount);
        Assert.Equal(40, vm.RemainingPrompts);
        Assert.Equal(40.0, vm.RemainingQuotaPercent);
        Assert.True(vm.BurnRatePromptsPerHour > 0);
        Assert.Contains("req/hr", vm.BurnRateFormatted);

        // Exhaustion: since 40 remaining and positive rate, it should forecast remaining hours/minutes
        Assert.Contains("At current velocity", vm.ExhaustionForecastText);
        Assert.False(vm.IsNearQuotaExhausted); // 40% > 20%
    }

    [Fact]
    public void ProfileItemViewModel_NearQuotaExhausted_TriggersWarningBanner()
    {
        // Arrange: remaining quota <= 20%
        var profile = new AccountProfile
        {
            Name = "NearExhaustedProfile",
            Tier = "Basic",
            AuthStatus = new ProfileAuthStatus
            {
                DailyQuotaLimit = 100,
                TodayTurnsCount = 85 // 15 remaining (15%)
            }
        };

        var launcher = new TerminalLauncherService();
        var authDetector = new AuthDetectorService();
        var audio = new AudioService { IsEnabled = false };
        var doctor = new ProfileDoctorService();

        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio, doctor);

        // Act & Assert
        Assert.Equal(15, vm.RemainingPrompts);
        Assert.Equal(15.0, vm.RemainingQuotaPercent);
        Assert.True(vm.IsNearQuotaExhausted);
        Assert.Contains("15% remaining", vm.QuotaToastAlertText);
        Assert.Equal("#F59E0B", vm.ExhaustionBadgeColor); // 15% is amber
    }

    [Fact]
    public void ProfileItemViewModel_DefaultProfile_CannotBeDeleted()
    {
        var defaultProfile = new AccountProfile
        {
            Name = "Default (Main Account)",
            CustomProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        var launcher = new TerminalLauncherService();
        var authDetector = new AuthDetectorService();
        var audio = new AudioService { IsEnabled = false };
        var doctor = new ProfileDoctorService();

        var vm = new ProfileItemViewModel(defaultProfile, launcher, authDetector, audio, doctor);

        Assert.True(vm.IsDefaultProfile);
        Assert.False(vm.CanDelete);
        Assert.Equal("Primary System Profile", vm.AccountRoleText);
        Assert.Equal("Profile Doctor", vm.DoctorButtonText);
    }

    [Fact]
    public void ProfileItemViewModel_WorkerProfile_CanBeDeleted()
    {
        var workerProfile = new AccountProfile
        {
            Name = "Worker Beta",
            CustomProfilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini-profiles", "worker-beta")
        };

        var launcher = new TerminalLauncherService();
        var authDetector = new AuthDetectorService();
        var audio = new AudioService { IsEnabled = false };
        var doctor = new ProfileDoctorService();

        var vm = new ProfileItemViewModel(workerProfile, launcher, authDetector, audio, doctor);

        Assert.False(vm.IsDefaultProfile);
        Assert.True(vm.CanDelete);
        Assert.Equal("Sandboxed Worker Profile", vm.AccountRoleText);
    }

    [Fact]
    public void SwarmFleetModelItem_DefaultsAndCalculations_AreValid()
    {
        var item = new SwarmFleetModelItem
        {
            ModelId = "claude-sonnet-4-6",
            DisplayName = "Claude Sonnet 4.6",
            Family = "Anthropic Claude",
            FamilyColor = "#8B5CF6",
            AssignedAccountsCount = 2,
            TotalPoolCapacity = 600,
            AggregateBurnRate = 24.5
        };

        Assert.True(item.IsActiveInSwarm);
        Assert.Equal("Active in Swarm", item.StatusBadgeText);
        Assert.Equal("600 prompts/day", item.PoolCapacityLabel);
        Assert.Equal("24.5 p/hr", item.BurnRateLabel);
    }
}
