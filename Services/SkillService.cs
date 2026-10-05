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

            var workspaceName = Path.GetFileName(currentWorkspace) ?? "Project Workspace";
            var workspaceSkillsDir = Path.Combine(currentWorkspace, ".agents", "skills");
            ScanDirectoryForSkills(workspaceSkillsDir, "Workspace", skills, seenPaths, workspaceName);

            // 2. User Built-in Skills (~/.gemini/antigravity-cli/builtin/skills)
            var builtinSkillsDir = Path.Combine(userHome, ".gemini", "antigravity-cli", "builtin", "skills");
            ScanDirectoryForSkills(builtinSkillsDir, "Built-in", skills, seenPaths, "Global CLI");

            // 3. User Plugin Skills (~/.gemini/config/plugins/*/skills/*)
            var pluginsDir = Path.Combine(userHome, ".gemini", "config", "plugins");
            if (Directory.Exists(pluginsDir))
            {
                try
                {
                    foreach (var pluginDir in Directory.GetDirectories(pluginsDir))
                    {
                        var pluginName = Path.GetFileName(pluginDir);
                        var pluginSkillsDir = Path.Combine(pluginDir, "skills");
                        ScanDirectoryForSkills(pluginSkillsDir, "Plugin", skills, seenPaths, pluginName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[SkillService] Error reading plugins directory '{pluginsDir}': {ex.Message}");
                }
            }

            // 4. Per-Profile Sandbox Skills (~/.gemini-profiles/*/.gemini/antigravity-cli/skills)
            var profilesDir = Path.Combine(userHome, ".gemini-profiles");
            if (Directory.Exists(profilesDir))
            {
                try
                {
                    foreach (var pDir in Directory.GetDirectories(profilesDir))
                    {
                        var profName = Path.GetFileName(pDir);
                        var customSkillsDir = Path.Combine(pDir, ".gemini", "antigravity-cli", "skills");
                        ScanDirectoryForSkills(customSkillsDir, "Profile", skills, seenPaths, profName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[SkillService] Error reading profiles directory '{profilesDir}': {ex.Message}");
                }
            }

            return skills.OrderBy(s => s.SourceType).ThenBy(s => s.Name).ToList();
        });
    }

    private void ScanDirectoryForSkills(string rootDir, string sourceType, List<SkillItem> skills, HashSet<string> seenPaths, string? ownerContext = null)
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

                    var skill = ParseSkillFile(skillFile, subDir, sourceType, ownerContext);
                    skills.Add(skill);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"[SkillService] Scan error in '{rootDir}': {ex.Message}");
        }
    }

    public static SkillItem ParseSkillFile(string skillFilePath, string skillDirectory, string sourceType, string? ownerContext = null)
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

        // Determine ownership and accessibility scope
        switch (sourceType)
        {
            case "Built-in":
                item.OwnerTitle = "Semua Akun (Global CLI Bawaan)";
                item.AccessibleBy = "Diwarisi otomatis oleh seluruh akun Google & worker swarm.";
                item.ScopeCategory = "Built-in";
                break;

            case "Workspace":
                var wsName = !string.IsNullOrWhiteSpace(ownerContext) ? ownerContext : "Project";
                item.OwnerTitle = $"Project: {wsName}";
                item.AccessibleBy = $"Hanya aktif saat akun/worker menjalankan tugas di workspace '{wsName}'.";
                item.ScopeCategory = "Workspace";
                break;

            case "Plugin":
                var pluginName = !string.IsNullOrWhiteSpace(ownerContext) ? ownerContext : "Plugin";
                item.OwnerTitle = $"Plugin: {pluginName} (Semua Akun)";
                item.AccessibleBy = $"Tersedia global untuk semua akun yang menggunakan plugin '{pluginName}'.";
                item.ScopeCategory = "Plugin";
                break;

            case "Profile":
                var profName = !string.IsNullOrWhiteSpace(ownerContext) ? ownerContext : "Profil Khusus";
                item.OwnerTitle = $"Profil: {profName}";
                item.AccessibleBy = $"Khusus untuk akun sandbox profil '{profName}'.";
                item.ScopeCategory = "Profile";
                break;

            default:
                item.OwnerTitle = "Semua Akun (Global)";
                item.AccessibleBy = "Dapat diakses oleh seluruh akun swarm.";
                item.ScopeCategory = "Global";
                break;
        }

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
