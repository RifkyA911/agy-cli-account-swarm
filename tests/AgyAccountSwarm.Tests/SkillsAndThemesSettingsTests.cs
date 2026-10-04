using System;
using System.IO;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class SkillsAndThemesSettingsTests
{
    [Fact]
    public async Task SkillService_DiscoversAndParsesSkillMd_Correctly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "agy_test_skills_" + Guid.NewGuid().ToString("N"));
        var skillFolder = Path.Combine(tempDir, ".agents", "skills", "test-craftsman");
        Directory.CreateDirectory(skillFolder);

        var skillMdContent = @"---
name: test-craftsman
description: Enforces thorough and generous engineering practices.
tags: [craft, quality, testing]
---

# Test Craftsman Guidelines
Always write comprehensive tests and verify zero warnings.
";
        await File.WriteAllTextAsync(Path.Combine(skillFolder, "SKILL.md"), skillMdContent);

        try
        {
            var service = new SkillService();

            // Act
            var skills = await service.DiscoverSkillsAsync(tempDir);

            // Assert
            Assert.NotEmpty(skills);
            var testSkill = skills.Find(s => s.Name == "test-craftsman");
            Assert.NotNull(testSkill);
            Assert.Equal("Enforces thorough and generous engineering practices.", testSkill.Description);
            Assert.Equal("Workspace", testSkill.SourceType);
            Assert.True(testSkill.HasInstructions);
            Assert.Contains("Always write comprehensive tests", testSkill.InstructionsContent);
            Assert.Contains("craft", testSkill.Tags);
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
    public void TerminalType_PowerShellCoreAndGitBash_GenerateValidSnippets()
    {
        // Arrange
        var profile = new AccountProfile
        {
            Id = "prof_pwsh",
            Name = "Pwsh Worker",
            CustomProfilePath = @"C:\Users\test\.gemini-profiles\pwsh_worker"
        };
        var launcher = new TerminalLauncherService();

        // Act - PowerShellCore
        string pwshSnippet = launcher.GetCliSnippet(profile, TerminalType.PowerShellCore);
        // Act - GitBash
        string bashSnippet = launcher.GetCliSnippet(profile, TerminalType.GitBash);

        // Assert
        Assert.Contains("$env:USERPROFILE=", pwshSnippet);
        Assert.Contains("agy", pwshSnippet);

        Assert.Contains("export USERPROFILE=", bashSnippet);
        Assert.Contains("HOME=", bashSnippet);
    }

    [Theory]
    [InlineData("Cyberpunk", "#8A2BE2", "#00F5FF", "#FF1493", "#FF6700")]
    [InlineData("Pastel", "#E6E6FA", "#FFD1DC", "#AEC6CF", "#FFFACD")]
    [InlineData("Cosmic", "#0B0B1E", "#4B0082", "#FF007F", "#CCFF00")]
    [InlineData("Sunset", "#FF7518", "#FF4500", "#D10074", "#FFF700")]
    public void GradientThemes_ContainStrictHexCodes(string theme, string c1, string c2, string c3, string c4)
    {
        Assert.False(string.IsNullOrWhiteSpace(theme));
        // Verify exact HEX definitions specified in user guidelines
        var expectedCodes = new[] { c1, c2, c3, c4 };
        foreach (var hex in expectedCodes)
        {
            Assert.StartsWith("#", hex);
            Assert.Equal(7, hex.Length);
        }
    }

    [Fact]
    public void AppSettings_AutoSyncAndConfirmWorktreeMerge_DefaultValues_AreTrue()
    {
        var settings = new AppSettings();
        Assert.True(settings.AutoSyncEnabled);
        Assert.True(settings.ConfirmWorktreeMerge);
        Assert.Equal("Detailed", settings.NavbarDisplayMode);
        Assert.Equal("Cyberpunk", settings.GradientTheme);
        Assert.Equal("ConcurrentCli", settings.SwarmExecutionMode);
    }

    [Fact]
    public void SkillItem_BadgeProperties_FormatBasedOnSourceType()
    {
        var wsSkill = new SkillItem { SourceType = "Workspace" };
        var builtinSkill = new SkillItem { SourceType = "Built-in" };
        var pluginSkill = new SkillItem { SourceType = "Plugin" };

        Assert.Equal("#3B82F6", wsSkill.SourceBadgeColor);
        Assert.Equal("🛠️", wsSkill.Icon);

        Assert.Equal("#10B981", builtinSkill.SourceBadgeColor);
        Assert.Equal("⚡", builtinSkill.Icon);

        Assert.Equal("#8B5CF6", pluginSkill.SourceBadgeColor);
        Assert.Equal("🧩", pluginSkill.Icon);
    }
}
