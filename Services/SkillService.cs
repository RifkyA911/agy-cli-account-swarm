using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface ISkillService
{
    Task<List<SkillItem>> DiscoverSkillsAsync(string? workspacePath = null);
}

public class SkillService : ISkillService
{
    public Task<List<SkillItem>> DiscoverSkillsAsync(string? workspacePath = null)
    {
        return Task.Run(() =>
        {
            var skills = new List<SkillItem>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // 1. Workspace Skills (.agents/skills)
            var currentWorkspace = !string.IsNullOrWhiteSpace(workspacePath) && Directory.Exists(workspacePath)
                ? workspacePath
                : Directory.GetCurrentDirectory();

            var workspaceSkillsDir = Path.Combine(currentWorkspace, ".agents", "skills");
            ScanDirectoryForSkills(workspaceSkillsDir, "Workspace", skills, seenPaths);

            // 2. User Built-in Skills (~/.gemini/antigravity-cli/builtin/skills)
            var builtinSkillsDir = Path.Combine(userHome, ".gemini", "antigravity-cli", "builtin", "skills");
            ScanDirectoryForSkills(builtinSkillsDir, "Built-in", skills, seenPaths);

            // 3. User Plugin Skills (~/.gemini/config/plugins/*/skills/*)
            var pluginsDir = Path.Combine(userHome, ".gemini", "config", "plugins");
            if (Directory.Exists(pluginsDir))
            {
                try
                {
                    foreach (var pluginDir in Directory.GetDirectories(pluginsDir))
                    {
                        var pluginSkillsDir = Path.Combine(pluginDir, "skills");
                        ScanDirectoryForSkills(pluginSkillsDir, "Plugin", skills, seenPaths);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[SkillService] Error reading plugins directory '{pluginsDir}': {ex.Message}");
                }
            }

            return skills.OrderBy(s => s.SourceType).ThenBy(s => s.Name).ToList();
        });
    }

    private void ScanDirectoryForSkills(string rootDir, string sourceType, List<SkillItem> skills, HashSet<string> seenPaths)
    {
        if (!Directory.Exists(rootDir)) return;

        try
        {
            foreach (var subDir in Directory.GetDirectories(rootDir))
            {
                var skillFile = Path.Combine(subDir, "SKILL.md");
                if (!File.Exists(skillFile))
                {
                    // Fallback to lowercase skill.md
                    skillFile = Path.Combine(subDir, "skill.md");
                }

                if (File.Exists(skillFile))
                {
                    var fullPath = Path.GetFullPath(skillFile);
                    if (seenPaths.Contains(fullPath)) continue;
                    seenPaths.Add(fullPath);

                    var skill = ParseSkillFile(skillFile, subDir, sourceType);
                    skills.Add(skill);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[SkillService] Scan error in '{rootDir}': {ex.Message}");
        }
    }

    public static SkillItem ParseSkillFile(string skillFilePath, string skillDirectory, string sourceType)
    {
        var dirName = Path.GetFileName(skillDirectory);
        var item = new SkillItem
        {
            Name = dirName,
            DirectoryPath = skillDirectory,
            SkillFilePath = skillFilePath,
            SourceType = sourceType,
            HasInstructions = true
        };

        try
        {
            var content = File.ReadAllText(skillFilePath);
            item.InstructionsContent = content;

            // Check for YAML Frontmatter: --- ... ---
            if (content.StartsWith("---"))
            {
                var secondDashIdx = content.IndexOf("---", 3, StringComparison.Ordinal);
                if (secondDashIdx > 0)
                {
                    var frontmatter = content.Substring(3, secondDashIdx - 3);
                    var remaining = content.Substring(secondDashIdx + 3).Trim();

                    // Parse name: and description:
                    var nameMatch = Regex.Match(frontmatter, @"^name:\s*([^\r\n]+)", RegexOptions.Multiline);
                    if (nameMatch.Success && !string.IsNullOrWhiteSpace(nameMatch.Groups[1].Value))
                    {
                        item.Name = nameMatch.Groups[1].Value.Trim('"', '\'', ' ');
                    }

                    var descMatch = Regex.Match(frontmatter, @"^description:\s*([^\r\n]+)", RegexOptions.Multiline);
                    if (descMatch.Success && !string.IsNullOrWhiteSpace(descMatch.Groups[1].Value))
                    {
                        item.Description = descMatch.Groups[1].Value.Trim('"', '\'', ' ');
                    }

                    var tagsMatch = Regex.Match(frontmatter, @"^tags:\s*\[([^\]]+)\]", RegexOptions.Multiline);
                    if (tagsMatch.Success)
                    {
                        var tagTokens = tagsMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var token in tagTokens)
                        {
                            var clean = token.Trim('"', '\'', ' ');
                            if (!string.IsNullOrWhiteSpace(clean) && !item.Tags.Contains(clean))
                            {
                                item.Tags.Add(clean);
                            }
                        }
                    }

                    item.InstructionsPreview = remaining.Length > 200
                        ? remaining.Substring(0, 200).Trim() + "..."
                        : remaining;
                }
            }

            // Fallback for description if not found in frontmatter
            if (string.IsNullOrWhiteSpace(item.Description))
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("#")) continue;
                    if (trimmed.StartsWith("---")) continue;
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        item.Description = trimmed.Length > 180 ? trimmed.Substring(0, 180) + "..." : trimmed;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(item.Description))
            {
                item.Description = $"Antigravity agent extension skill from {sourceType} catalog.";
            }

            // Extract tags
            item.Tags.Add(sourceType);
            if (item.Name.Contains("-"))
            {
                var parts = item.Name.Split('-', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1) item.Tags.Add(parts[0]);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SkillService] Failed parsing skill '{skillFilePath}': {ex.Message}");
            item.Description = "Custom Antigravity Agent Skill";
        }

        return item;
    }
}
