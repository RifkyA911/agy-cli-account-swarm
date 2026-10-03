using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public class SwarmAggregatorService : ISwarmAggregatorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly ConcurrentDictionary<string, List<SwarmChatMessage>> _projectMessagesCache = new();

    public string ResolveWorkerProjectDirectory(AccountProfile worker, SwarmProject project)
    {
        var sanitizedProjName = SwarmProject.SanitizeProjectFolderName(project.Name);

        // If worker has custom workspace or profile dir, nest the project inside it
        string baseDir;
        if (!string.IsNullOrWhiteSpace(worker.DefaultWorkspace) && Directory.Exists(worker.DefaultWorkspace))
        {
            baseDir = worker.DefaultWorkspace;
        }
        else
        {
            baseDir = Path.Combine(worker.GetEffectiveProfileDirectory().TrimEnd('\\', '/'), "workspace");
        }

        var projectWorkDir = Path.Combine(baseDir, sanitizedProjName);
        if (!Directory.Exists(projectWorkDir))
        {
            Directory.CreateDirectory(projectWorkDir);
            Logger.Info($"[SwarmAggregator] Created dedicated project workspace for '{worker.Name}': '{projectWorkDir}'");
        }

        return projectWorkDir;
    }

    public string EnsureProjectSwarmWorkspace(SwarmProject project, IEnumerable<AccountProfile> workers)
    {
        var workerList = workers.ToList();
        string rootDir = !string.IsNullOrWhiteSpace(project.RootDirectory) && Directory.Exists(project.RootDirectory)
            ? project.RootDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SwarmProjects", SwarmProject.SanitizeProjectFolderName(project.Name));

        if (!Directory.Exists(rootDir))
        {
            Directory.CreateDirectory(rootDir);
        }

        var swarmDir = Path.Combine(rootDir, ".swarm");
        if (!Directory.Exists(swarmDir))
        {
            Directory.CreateDirectory(swarmDir);
        }

        // 1. Initialize or update manifest.json
        var manifestFile = Path.Combine(swarmDir, "manifest.json");
        var manifestData = new
        {
            projectId = project.Id,
            projectName = project.Name,
            techStack = project.TechStack,
            rootDirectory = rootDir,
            createdAt = project.CreatedAt,
            workers = workerList.Select((w, idx) => new
            {
                id = w.Id,
                name = w.Name,
                role = FleetDispatcherService.ResolveWorkerRole(w, idx),
                email = w.AccountEmail,
                tier = w.Tier
            })
        };

        File.WriteAllText(manifestFile, JsonSerializer.Serialize(manifestData, IndentedJsonOptions), new UTF8Encoding(false));

        // 2. Initialize bus.jsonl if absent
        var busFile = Path.Combine(swarmDir, "bus.jsonl");
        if (!File.Exists(busFile))
        {
            var initMsg = new SwarmChatMessage
            {
                ProjectId = project.Id,
                SenderName = "Swarm Orchestrator",
                SenderRole = "System",
                SenderColor = "#10B981",
                Content = $"🚀 Project '{project.Name}' initialized. Tech Stack: {project.TechStack}. Participating workers: {workerList.Count}.",
                Type = SwarmMessageType.SystemEvent,
                Timestamp = DateTime.UtcNow
            };
            var line = JsonSerializer.Serialize(initMsg, JsonOptions);
            File.WriteAllText(busFile, line + Environment.NewLine, new UTF8Encoding(false));
        }

        // 3. Initialize blackboard.md if absent
        var blackboardFile = Path.Combine(swarmDir, "blackboard.md");
        if (!File.Exists(blackboardFile))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Swarm Project Blackboard: {project.Name}");
            sb.AppendLine();
            sb.AppendLine($"- **Tech Stack**: `{project.TechStack}`");
            sb.AppendLine($"- **Root Path**: `{rootDir}`");
            sb.AppendLine($"- **Created**: {project.CreatedAt:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine();
            sb.AppendLine("## 👥 Active Worker Matrix");
            sb.AppendLine("| Worker Account | Assigned Domain Role | Tier | Sandbox Workspace |");
            sb.AppendLine("|---|---|---|---|");
            for (int i = 0; i < workerList.Count; i++)
            {
                var w = workerList[i];
                var role = FleetDispatcherService.ResolveWorkerRole(w, i);
                var wDir = ResolveWorkerProjectDirectory(w, project);
                sb.AppendLine($"| {w.Name} | **{role}** | {w.Tier} | `{wDir}` |");
            }
            sb.AppendLine();
            sb.AppendLine("## 📋 Shared Architecture & Inter-Agent Contracts");
            sb.AppendLine("All workers should write exported APIs, interfaces, and completion signals to this blackboard or append messages to `.swarm/bus.jsonl`.");
            sb.AppendLine();
            sb.AppendLine("### Recent Logs & Broadcasts");
            sb.AppendLine($"- [{DateTime.UtcNow:HH:mm:ss}] System: Swarm initialized.");

            File.WriteAllText(blackboardFile, sb.ToString(), new UTF8Encoding(false));
        }

        // 4. Ensure each worker's workspace has access to .swarm/
        foreach (var worker in workerList)
        {
            var workerDir = ResolveWorkerProjectDirectory(worker, project);
            var workerSwarmDir = Path.Combine(workerDir, ".swarm");
            if (!Directory.Exists(workerSwarmDir) && !string.Equals(workerDir, rootDir, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.CreateDirectory(workerSwarmDir);
                    // Copy manifest and pointer
                    File.Copy(manifestFile, Path.Combine(workerSwarmDir, "manifest.json"), overwrite: true);
                    File.WriteAllText(Path.Combine(workerSwarmDir, "swarm-root.txt"), rootDir, new UTF8Encoding(false));
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[SwarmAggregator] Worker swarm link skipped: {ex.Message}");
                }
            }
        }

        return rootDir;
    }

    public async Task PostMessageAsync(SwarmProject project, SwarmChatMessage message)
    {
        message.ProjectId = project.Id;
        message.Timestamp = DateTime.UtcNow;

        // In-memory cache
        var list = _projectMessagesCache.GetOrAdd(project.Id, _ => new List<SwarmChatMessage>());
        lock (list)
        {
            list.Add(message);
        }

        // Append to .swarm/bus.jsonl
        try
        {
            string rootDir = !string.IsNullOrWhiteSpace(project.RootDirectory) && Directory.Exists(project.RootDirectory)
                ? project.RootDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SwarmProjects", SwarmProject.SanitizeProjectFolderName(project.Name));

            var swarmDir = Path.Combine(rootDir, ".swarm");
            if (!Directory.Exists(swarmDir)) Directory.CreateDirectory(swarmDir);

            var busFile = Path.Combine(swarmDir, "bus.jsonl");
            var line = JsonSerializer.Serialize(message, JsonOptions);
            await File.AppendAllTextAsync(busFile, line + Environment.NewLine, new UTF8Encoding(false));

            // Also append highlight to blackboard.md
            var blackboardFile = Path.Combine(swarmDir, "blackboard.md");
            if (File.Exists(blackboardFile) && message.Type is SwarmMessageType.UserBroadcast or SwarmMessageType.Handoff or SwarmMessageType.SystemEvent)
            {
                var entry = $"- [{message.TimestampFormatted}] **{message.SenderName}** ({message.SenderRole}): {message.Content}{Environment.NewLine}";
                await File.AppendAllTextAsync(blackboardFile, entry, new UTF8Encoding(false));
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SwarmAggregator] Failed to append message to bus: {ex.Message}");
        }
    }

    public async Task<List<SwarmChatMessage>> LoadProjectMessagesAsync(SwarmProject project)
    {
        string rootDir = !string.IsNullOrWhiteSpace(project.RootDirectory) && Directory.Exists(project.RootDirectory)
            ? project.RootDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SwarmProjects", SwarmProject.SanitizeProjectFolderName(project.Name));

        var busFile = Path.Combine(rootDir, ".swarm", "bus.jsonl");
        if (!File.Exists(busFile))
        {
            return _projectMessagesCache.TryGetValue(project.Id, out var cached) ? cached.ToList() : new List<SwarmChatMessage>();
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(busFile);
            var result = new List<SwarmChatMessage>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var msg = JsonSerializer.Deserialize<SwarmChatMessage>(line, JsonOptions);
                    if (msg != null) result.Add(msg);
                }
                catch { }
            }

            _projectMessagesCache[project.Id] = result;
            return result;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SwarmAggregator] Failed to load messages: {ex.Message}");
            return _projectMessagesCache.TryGetValue(project.Id, out var cached) ? cached.ToList() : new List<SwarmChatMessage>();
        }
    }

    public async Task<SwarmChatMessage> BroadcastUserInstructionAsync(SwarmProject project, string userText, string? targetWorker = null)
    {
        var msg = new SwarmChatMessage
        {
            ProjectId = project.Id,
            SenderName = "You (User)",
            SenderRole = "Commander",
            SenderColor = "#3B82F6",
            Content = userText.Trim(),
            Type = SwarmMessageType.UserBroadcast,
            TargetWorker = targetWorker,
            AvatarInitial = "U"
        };

        await PostMessageAsync(project, msg);
        Logger.Info($"[SwarmAggregator] User broadcast posted to '{project.Name}' (Target: {targetWorker ?? "All"})");
        return msg;
    }

    public async Task PostAgentActionTelemetryAsync(
        SwarmProject project,
        string workerName,
        string role,
        string line,
        string? colorTag = null,
        string? avatarUrl = null,
        string? avatarInitial = null)
    {
        var parsedActivity = FleetDispatcherService.ParseActivityFromOutput(line);
        if (string.IsNullOrWhiteSpace(parsedActivity)) return;

        // Skip excessive noisy lines to keep the chat legible
        if (line.Length > 200) line = line.Substring(0, 197) + "...";

        var msg = new SwarmChatMessage
        {
            ProjectId = project.Id,
            SenderName = workerName,
            SenderRole = role,
            SenderColor = string.IsNullOrWhiteSpace(colorTag) ? "#10B981" : colorTag,
            AvatarUrl = avatarUrl,
            AvatarInitial = !string.IsNullOrEmpty(avatarInitial) ? avatarInitial : workerName.Substring(0, 1).ToUpperInvariant(),
            Content = $"{parsedActivity}: {line}",
            Type = SwarmMessageType.AgentAction,
            Timestamp = DateTime.UtcNow
        };

        await PostMessageAsync(project, msg);
    }

    public async Task UpdateBlackboardAsync(SwarmProject project, string updateContent, string author = "System")
    {
        string rootDir = !string.IsNullOrWhiteSpace(project.RootDirectory) && Directory.Exists(project.RootDirectory)
            ? project.RootDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SwarmProjects", SwarmProject.SanitizeProjectFolderName(project.Name));

        var blackboardFile = Path.Combine(rootDir, ".swarm", "blackboard.md");
        try
        {
            var entry = $"- [{DateTime.UtcNow:HH:mm:ss}] **{author}**: {updateContent}{Environment.NewLine}";
            await File.AppendAllTextAsync(blackboardFile, entry, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SwarmAggregator] Failed to update blackboard: {ex.Message}");
        }
    }
}
