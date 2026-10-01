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
}

