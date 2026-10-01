using System.Globalization;
using System.IO;
using System.Linq;
using AgyAccountSwarm.Converters;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using AgyAccountSwarm.ViewModels;
using Xunit;


namespace AgyAccountSwarm.Tests;

public class UiAndViewModelEnhancementsTests
{
    [Fact]
    public void BooleanToChevronConverter_ConvertsAccurately()
    {
        var converter = new BooleanToChevronConverter();

        var expanded = converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);
        var collapsed = converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Equal("▼", expanded);
        Assert.Equal("▶", collapsed);
    }

    [Fact]
    public void ProfileItemViewModel_AccordionToggles_WorkCorrectly()
    {
        var launcher = new TerminalLauncherService();
        var authDetector = new AuthDetectorService();
        var audio = new AudioService { IsEnabled = false };

        var profile = new AccountProfile { Name = "TestProfile" };
        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio);

        // Initial defaults
        Assert.True(vm.IsDailyQuotaExpanded);
        Assert.True(vm.IsModelQuotasExpanded);
        Assert.False(vm.IsCliInspectorExpanded);

        // Toggle commands
        vm.ToggleDailyQuotaSectionCommand.Execute(null);
        Assert.False(vm.IsDailyQuotaExpanded);

        vm.ToggleModelQuotasSectionCommand.Execute(null);
        Assert.False(vm.IsModelQuotasExpanded);

        vm.ToggleCliInspectorSectionCommand.Execute(null);
        Assert.True(vm.IsCliInspectorExpanded);
    }

    [Fact]
    public void ProfileItemViewModel_ModelQuotasColors_ReflectRemainingCapacity()
    {
        var launcher = new TerminalLauncherService();
        var authDetector = new AuthDetectorService();
        var audio = new AudioService { IsEnabled = false };

        var profile = new AccountProfile
        {
            Name = "TestProfile",
            AuthStatus = new ProfileAuthStatus
            {
                GeminiWeeklyRemainingPercent = 65.0,  // Green (> 50%)
                Gemini5HourRemainingPercent = 35.0,   // Amber (20-50%)
                ClaudeGptWeeklyRemainingPercent = 15.0, // Red (<= 20%)
                ClaudeGpt5HourRemainingPercent = 0.0   // Red (<= 20%)
            }
        };

        var vm = new ProfileItemViewModel(profile, launcher, authDetector, audio);

        Assert.Equal("#10B981", vm.GeminiWeeklyBarColor);
        Assert.Equal("#F59E0B", vm.Gemini5HourBarColor);
        Assert.Equal("#EF4444", vm.ClaudeGptWeeklyBarColor);
        Assert.Equal("#EF4444", vm.ClaudeGpt5HourBarColor);
    }

    [Fact]
    public void MainWindowXaml_AllStaticResources_MustResolve()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "AgyAccountSwarm.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        var projectDir = dir;
        var mainWindowXamlPath = Path.Combine(projectDir, "Views", "MainWindow.xaml");
        var appXamlPath = Path.Combine(projectDir, "App.xaml");
        var themeXamlPath = Path.Combine(projectDir, "Resources", "Theme.xaml");


        Assert.True(File.Exists(mainWindowXamlPath), $"MainWindow.xaml not found at {mainWindowXamlPath}");
        Assert.True(File.Exists(appXamlPath), $"App.xaml not found at {appXamlPath}");
        Assert.True(File.Exists(themeXamlPath), $"Theme.xaml not found at {themeXamlPath}");

        var mainWindowContent = File.ReadAllText(mainWindowXamlPath);
        var combinedResourceText = File.ReadAllText(appXamlPath) + " " + File.ReadAllText(themeXamlPath) + " " + mainWindowContent;

        var regex = new System.Text.RegularExpressions.Regex(@"StaticResource\s+([A-Za-z0-9_]+)");
        var matches = regex.Matches(mainWindowContent);
        var distinctKeys = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

        var missing = new System.Collections.Generic.List<string>();
        foreach (var key in distinctKeys)
        {
            var pattern = "x:Key=\"" + key + "\"";
            if (!combinedResourceText.Contains(pattern))
            {
                missing.Add(key);
            }
        }

        Assert.True(missing.Count == 0, $"Missing StaticResource definitions in XAML: {string.Join(", ", missing)}");
    }

    [Fact]
    public void AccountProfile_RichContextProperties_FormatAccurately()
    {
        var profile = new AccountProfile
        {
            Name = "Alpha Worker",
            Tier = "Pro",
            QuotaLimit = 1000,
            PreferredModel = "gemini-2.5-pro",
            AuthStatus = new ProfileAuthStatus
            {
                AccountEmail = "alpha.worker@gmail.com",
                Status = AuthStatusType.Authenticated,
                TodayTurnsCount = 42,
                GeminiWeeklyRemainingPercent = 75.5
            }
        };

        Assert.Equal("A", profile.AvatarInitial);
        Assert.Equal("alpha.worker@gmail.com", profile.AccountEmail);
        Assert.Equal("Pro", profile.TierBadgeText);
        Assert.Equal("#7C3AED", profile.TierBadgeBackground);
        Assert.Equal("#10B981", profile.StatusBadgeColor);
        Assert.Equal("alpha.worker@gmail.com", profile.StatusBadgeText);
        Assert.Equal("42 / 1000 prompts today", profile.TodayQuotaFormatted);
        Assert.Equal("76% quota remaining", profile.WeeklyRemainingFormatted);
    }

    [Fact]
    public async Task AgyUsageParser_AllowCliSpawnFalse_NeverSpawnsProcess()
    {
        // Calling with allowCliSpawn: false and non-existent/uncached directory should return null immediately without spawning
        var dummyPath = Path.Combine(Path.GetTempPath(), "agy_dummy_profile_" + Guid.NewGuid().ToString("N"));
        var result = await AgyUsageParser.FetchUsageCachedAsync(dummyPath, false, "invalid_nonexistent_agy.exe", allowCliSpawn: false);
        Assert.Null(result);
    }

    [Fact]
    public void AvaloniaMainWindowAxaml_StaticResources_MustResolve()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "AgyAccountSwarm.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        var projectDir = dir;
        var avaloniaAxamlPath = Path.Combine(projectDir, "AgyAccountSwarm.Avalonia", "MainWindow.axaml");
        var appAxamlPath = Path.Combine(projectDir, "AgyAccountSwarm.Avalonia", "App.axaml");

        Assert.True(File.Exists(avaloniaAxamlPath), $"Avalonia MainWindow.axaml not found at {avaloniaAxamlPath}");
        Assert.True(File.Exists(appAxamlPath), $"Avalonia App.axaml not found at {appAxamlPath}");

        var mainWindowContent = File.ReadAllText(avaloniaAxamlPath);
        var combinedResourceText = File.ReadAllText(appAxamlPath) + " " + mainWindowContent;

        var regex = new System.Text.RegularExpressions.Regex(@"StaticResource\s+([A-Za-z0-9_]+)");
        var matches = regex.Matches(mainWindowContent);
        var distinctKeys = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

        var missing = new System.Collections.Generic.List<string>();
        foreach (var key in distinctKeys)
        {
            var pattern = "x:Key=\"" + key + "\"";
            if (!combinedResourceText.Contains(pattern))
            {
                missing.Add(key);
            }
        }

        Assert.True(missing.Count == 0, $"Missing StaticResource definitions in Avalonia AXAML: {string.Join(", ", missing)}");
    }
}

