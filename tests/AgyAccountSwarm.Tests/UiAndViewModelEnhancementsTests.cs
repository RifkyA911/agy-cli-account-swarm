using System.Globalization;
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
}
