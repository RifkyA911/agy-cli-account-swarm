using System.Linq;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.ViewModels;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class ProfileEditViewModelTests
{
    [Fact]
    public void SelectColor_UpdatesSelectedColor_AndMarksOptionSelected()
    {
        // Arrange
        var vm = new ProfileEditViewModel();
        var emeraldOption = vm.ColorPresets.FirstOrDefault(c => c.Name == "Emerald");
        Assert.NotNull(emeraldOption);

        // Act
        vm.SelectColorCommand.Execute(emeraldOption);

        // Assert
        Assert.Equal("#10B981", vm.SelectedColor);
        Assert.True(emeraldOption.IsSelected);

        // Ensure other options are not selected
        var blueOption = vm.ColorPresets.FirstOrDefault(c => c.Name == "Blue");
        Assert.NotNull(blueOption);
        Assert.False(blueOption.IsSelected);
    }

    [Fact]
    public void SelectColor_WithStringHex_UpdatesSelectedColor()
    {
        // Arrange
        var vm = new ProfileEditViewModel();

        // Act
        vm.SelectColorCommand.Execute("#8B5CF6"); // Violet

        // Assert
        Assert.Equal("#8B5CF6", vm.SelectedColor);
        var violet = vm.ColorPresets.FirstOrDefault(c => c.Hex == "#8B5CF6");
        Assert.NotNull(violet);
        Assert.True(violet.IsSelected);
    }

    [Theory]
    [InlineData("Basic", 100)]
    [InlineData("Plus", 300)]
    [InlineData("Pro", 1000)]
    [InlineData("Ultra", 2500)]
    public void OnTierChanged_UpdatesDailyQuotaLimitAutomatically(string tier, int expectedQuota)
    {
        // Arrange
        var vm = new ProfileEditViewModel();

        // Act
        vm.Tier = tier;

        // Assert
        Assert.Equal(expectedQuota, vm.QuotaLimit);
    }

    [Fact]
    public void ModelSelection_UpdatesPreferredModel_AndPreservesCustomModel()
    {
        // Arrange
        var discovered = new[] { "gemini-3.8-flash-high", "custom-community-model" };
        var vm = new ProfileEditViewModel(discovered);

        // Act
        vm.PreferredModel = "custom-community-model";

        // Assert
        Assert.Equal("custom-community-model", vm.PreferredModel);
        Assert.Contains("custom-community-model", vm.ModelOptions);
    }

    [Fact]
    public void Save_EmptyName_ProducesValidationError()
    {
        // Arrange
        var vm = new ProfileEditViewModel();
        vm.Name = "   ";

        // Act
        vm.Save();

        // Assert
        Assert.NotNull(vm.ValidationError);
        Assert.Contains("required", vm.ValidationError, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Save_ValidProfile_AppliesAllConfigAndCloses()
    {
        // Arrange
        var vm = new ProfileEditViewModel();
        vm.Name = "Worker Alpha";
        vm.Description = "Deep coding swarm agent";
        vm.Tier = "Pro";
        vm.PreferredModel = "Gemini 3.8 Flash (Medium)";
        vm.SelectedColor = "#10B981";
        vm.IsQuotaExhausted = false;

        bool closeTriggered = false;
        vm.RequestClose += success => closeTriggered = success;

        // Act
        vm.Save();

        // Assert
        Assert.True(closeTriggered);
        Assert.Null(vm.ValidationError);
        Assert.Equal("Worker Alpha", vm.ResultProfile.Name);
        Assert.Equal("Deep coding swarm agent", vm.ResultProfile.Description);
        Assert.Equal("Pro", vm.ResultProfile.Tier);
        Assert.Equal("Gemini 3.8 Flash (Medium)", vm.ResultProfile.PreferredModel);
        Assert.Equal("#10B981", vm.ResultProfile.ColorTag);
        Assert.Equal(1000, vm.ResultProfile.QuotaLimit);
    }
}
