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

    public static string ResolveProjectRootDir(SwarmProject project)
    {
        return !string.IsNullOrWhiteSpace(project.RootDirectory) && Directory.Exists(project.RootDirectory)
            ? project.RootDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SwarmProjects", SwarmProject.SanitizeProjectFolderName(project.Name));
    }

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
        string rootDir = ResolveProjectRootDir(project);

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
                SenderColor = "#4F46E5",
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

    public static SwarmChatMessage? ParseMessageLine(string line, string projectId)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        try
        {
            var msg = JsonSerializer.Deserialize<SwarmChatMessage>(line, JsonOptions);
            if (msg != null)
            {
                if (string.IsNullOrWhiteSpace(msg.ProjectId)) msg.ProjectId = projectId;
                return msg;
            }
        }
        catch (JsonException)
        {
            // Defensive Fallback: Parse non-JSON lines emitted by external CLI tools, shells, or humans
            var trimmed = line.Trim();
            string sender = "External Agent";
            string role = "Worker";
            string content = trimmed;

            // Pattern: [SenderName (Role)]: Content or [SenderName]: Content
            if (trimmed.StartsWith("[") && trimmed.Contains("]"))
            {
                int closeBracket = trimmed.IndexOf(']');
                var header = trimmed.Substring(1, closeBracket - 1).Trim();
                var remainder = trimmed.Substring(closeBracket + 1).TrimStart(':', ' ', '-');

                if (header.Contains("(") && header.EndsWith(")"))
                {
                    int openParen = header.IndexOf('(');
                    sender = header.Substring(0, openParen).Trim();
                    role = header.Substring(openParen + 1, header.Length - openParen - 2).Trim();
                }
                else
                {
                    sender = header;
                }

                if (!string.IsNullOrWhiteSpace(remainder))
                {
                    content = remainder;
                }
            }

            return new SwarmChatMessage
            {
                ProjectId = projectId,
                SenderName = string.IsNullOrWhiteSpace(sender) ? "External Agent" : sender,
                SenderRole = string.IsNullOrWhiteSpace(role) ? "Worker" : role,
                SenderColor = "#10B981",
                AvatarInitial = !string.IsNullOrEmpty(sender) ? sender.Substring(0, 1).ToUpperInvariant() : "A",
                Content = content,
                Type = SwarmMessageType.AgentAction,
                Timestamp = DateTime.UtcNow
            };
        }
        catch
        {
            // Ignore corrupted line
        }

        return null;
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

        // Concurrency-safe append to .swarm/bus.jsonl
        try
        {
            string rootDir = ResolveProjectRootDir(project);
            var swarmDir = Path.Combine(rootDir, ".swarm");
            if (!Directory.Exists(swarmDir)) Directory.CreateDirectory(swarmDir);

            var busFile = Path.Combine(swarmDir, "bus.jsonl");
            var line = JsonSerializer.Serialize(message, JsonOptions);

            using (var stream = new FileStream(busFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                await writer.WriteLineAsync(line);
            }

            // Also append highlight to blackboard.md
            var blackboardFile = Path.Combine(swarmDir, "blackboard.md");
            if (File.Exists(blackboardFile) && message.Type is SwarmMessageType.UserBroadcast or SwarmMessageType.Handoff or SwarmMessageType.SystemEvent)
            {
                var entry = $"- [{message.TimestampFormatted}] **{message.SenderName}** ({message.SenderRole}): {message.Content}";
                using var stream = new FileStream(blackboardFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteLineAsync(entry);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SwarmAggregator] Failed to append message to bus: {ex.Message}");
        }
    }

    public async Task<List<SwarmChatMessage>> LoadProjectMessagesAsync(SwarmProject project)
    {
        string rootDir = ResolveProjectRootDir(project);
        var busFile = Path.Combine(rootDir, ".swarm", "bus.jsonl");
        if (!File.Exists(busFile))
        {
            return _projectMessagesCache.TryGetValue(project.Id, out var cached) ? cached.ToList() : new List<SwarmChatMessage>();
        }

        try
        {
            var result = new List<SwarmChatMessage>();
            using (var stream = new FileStream(busFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var msg = ParseMessageLine(line, project.Id);
                    if (msg != null) result.Add(msg);
                }
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
        string rootDir = ResolveProjectRootDir(project);
        var blackboardFile = Path.Combine(rootDir, ".swarm", "blackboard.md");
        try
        {
            var entry = $"- [{DateTime.UtcNow:HH:mm:ss}] **{author}**: {updateContent.Trim()}";
            using var stream = new FileStream(blackboardFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteLineAsync(entry);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SwarmAggregator] Failed to update blackboard: {ex.Message}");
        }
    }

    public async Task<string> ReadBlackboardAsync(SwarmProject project)
    {
        string rootDir = ResolveProjectRootDir(project);
        var blackboardFile = Path.Combine(rootDir, ".swarm", "blackboard.md");
        if (!File.Exists(blackboardFile)) return string.Empty;

        try
        {
            using var stream = new FileStream(blackboardFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Logger.Warn($"[SwarmAggregator] Failed to read blackboard: {ex.Message}");
            return string.Empty;
        }
    }

    public IDisposable SubscribeProjectBus(SwarmProject project, Action<SwarmChatMessage> onNewMessage)
    {
        var rootDir = ResolveProjectRootDir(project);
        var swarmDir = Path.Combine(rootDir, ".swarm");
        if (!Directory.Exists(swarmDir))
        {
            Directory.CreateDirectory(swarmDir);
        }

        return new ProjectBusWatcher(project.Id, swarmDir, onNewMessage);
    }

    private sealed class ProjectBusWatcher : IDisposable
    {
        private readonly string _projectId;
        private readonly string _busPath;
        private readonly Action<SwarmChatMessage> _onNewMessage;
        private readonly FileSystemWatcher? _watcher;
        private readonly object _lock = new();
        private long _lastReadOffset = 0;
        private System.Threading.Timer? _debounceTimer;

        public ProjectBusWatcher(string projectId, string swarmDir, Action<SwarmChatMessage> onNewMessage)
        {
            _projectId = projectId;
            _busPath = Path.Combine(swarmDir, "bus.jsonl");
            _onNewMessage = onNewMessage;

            if (File.Exists(_busPath))
            {
                try
                {
                    _lastReadOffset = new FileInfo(_busPath).Length;
                }
                catch
                {
                    _lastReadOffset = 0;
                }
            }

            try
            {
                _watcher = new FileSystemWatcher(swarmDir, "bus.jsonl")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };

                _watcher.Changed += OnBusFileChanged;
                _watcher.Created += OnBusFileChanged;
            }
            catch (Exception ex)
            {
                Logger.Warn($"[SwarmAggregator] FileSystemWatcher could not be initialized for '{swarmDir}': {ex.Message}");
            }
        }

        private void OnBusFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(_ => ReadAppendedLines(), null, 80, System.Threading.Timeout.Infinite);
            }
        }

        private void ReadAppendedLines()
        {
            lock (_lock)
            {
                if (!File.Exists(_busPath)) return;

                try
                {
                    using var stream = new FileStream(_busPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length <= _lastReadOffset)
                    {
                        if (stream.Length < _lastReadOffset)
                        {
                            _lastReadOffset = 0; // File was truncated/recreated
                        }
                        else
                        {
                            return;
                        }
                    }

                    stream.Seek(_lastReadOffset, SeekOrigin.Begin);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var msg = ParseMessageLine(line, _projectId);
                        if (msg != null)
                        {
                            _onNewMessage(msg);
                        }
                    }

                    _lastReadOffset = stream.Position;
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[SwarmAggregator] Error reading appended bus lines: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Changed -= OnBusFileChanged;
                    _watcher.Created -= OnBusFileChanged;
                    _watcher.Dispose();
                }
            }
        }
    }
}
