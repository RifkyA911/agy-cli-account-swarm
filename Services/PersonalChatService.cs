using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface IPersonalChatService
{
    Task<List<PersonalChatSession>> GetSessionsAsync(string profileId, CancellationToken cancellationToken = default);
    Task<List<PersonalChatMessage>> LoadMessagesAsync(string profileId, string sessionId, CancellationToken cancellationToken = default);
    Task SaveSessionAsync(PersonalChatSession session, List<PersonalChatMessage> messages, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(string profileId, string sessionId, CancellationToken cancellationToken = default);
    Task<PersonalChatMessage> SendMessageAsync(
        AccountProfile profile,
        string prompt,
        string model = "gemini-2.5-pro",
        string effort = "medium",
        string? conversationId = null,
        List<RagChunkItem>? ragChunks = null,
        string? customAgyPath = null,
        CancellationToken cancellationToken = default);
    string GetRealtimeSwarmTaskStatusSummary(string workspaceRoot);
}

public class PersonalChatService : IPersonalChatService
{
    private readonly string _storageBaseDir;

    public PersonalChatService(string? storageBaseDir = null)
    {
        _storageBaseDir = storageBaseDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AgyAccountSwarm",
            "personal_chats");

        try
        {
            Directory.CreateDirectory(_storageBaseDir);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to create personal chat storage directory: {ex.Message}");
        }
    }

    private string GetProfileDir(string profileId)
    {
        var sanitized = Path.GetInvalidFileNameChars().Aggregate(profileId, (current, c) => current.Replace(c, '_'));
        var dir = Path.Combine(_storageBaseDir, sanitized);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public async Task<List<PersonalChatSession>> GetSessionsAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var list = new List<PersonalChatSession>();
        var dir = GetProfileDir(profileId);
        var sessionsFile = Path.Combine(dir, "sessions.json");

        if (File.Exists(sessionsFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(sessionsFile, cancellationToken);
                var items = JsonSerializer.Deserialize<List<PersonalChatSession>>(json);
                if (items != null) list.AddRange(items.OrderByDescending(s => s.UpdatedAt));
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to load sessions for profile {profileId}: {ex.Message}");
            }
        }

        if (list.Count == 0)
        {
            var defaultSession = new PersonalChatSession
            {
                Id = Guid.NewGuid().ToString(),
                ProfileId = profileId,
                Title = "General Assistant Chat",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TurnCount = 0
            };
            list.Add(defaultSession);
            await SaveSessionIndexAsync(dir, list, cancellationToken);
        }

        return list;
    }

    public async Task<List<PersonalChatMessage>> LoadMessagesAsync(string profileId, string sessionId, CancellationToken cancellationToken = default)
    {
        var dir = GetProfileDir(profileId);
        var msgFile = Path.Combine(dir, $"{sessionId}.json");

        if (File.Exists(msgFile))
        {
            try
            {
                var json = await File.ReadAllTextAsync(msgFile, cancellationToken);
                var items = JsonSerializer.Deserialize<List<PersonalChatMessage>>(json);
                if (items != null) return items;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to load messages for session {sessionId}: {ex.Message}");
            }
        }

        return new List<PersonalChatMessage>();
    }

    public async Task SaveSessionAsync(PersonalChatSession session, List<PersonalChatMessage> messages, CancellationToken cancellationToken = default)
    {
        var dir = GetProfileDir(session.ProfileId);
        var msgFile = Path.Combine(dir, $"{session.Id}.json");

        try
        {
            var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(msgFile, json, cancellationToken);

            var sessions = await GetSessionsAsync(session.ProfileId, cancellationToken);
            var existing = sessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing != null)
            {
                existing.Title = session.Title;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.TurnCount = messages.Count(m => m.IsUser);
                existing.LastQuerySnippet = messages.LastOrDefault(m => m.IsUser)?.Content ?? "";
                existing.Model = session.Model;
            }
            else
            {
                session.UpdatedAt = DateTime.UtcNow;
                session.TurnCount = messages.Count(m => m.IsUser);
                session.LastQuerySnippet = messages.LastOrDefault(m => m.IsUser)?.Content ?? "";
                sessions.Insert(0, session);
            }

            await SaveSessionIndexAsync(dir, sessions, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to save session {session.Id}: {ex.Message}");
        }
    }

    public async Task DeleteSessionAsync(string profileId, string sessionId, CancellationToken cancellationToken = default)
    {
        var dir = GetProfileDir(profileId);
        var msgFile = Path.Combine(dir, $"{sessionId}.json");

        try
        {
            if (File.Exists(msgFile)) File.Delete(msgFile);

            var sessions = await GetSessionsAsync(profileId, cancellationToken);
            sessions.RemoveAll(s => s.Id == sessionId);
            await SaveSessionIndexAsync(dir, sessions, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to delete session {sessionId}: {ex.Message}");
        }
    }

    private async Task SaveSessionIndexAsync(string dir, List<PersonalChatSession> sessions, CancellationToken cancellationToken)
    {
        try
        {
            var sessionsFile = Path.Combine(dir, "sessions.json");
            var json = JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(sessionsFile, json, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to save sessions index: {ex.Message}");
        }
    }

    public async Task<PersonalChatMessage> SendMessageAsync(
        AccountProfile profile,
        string prompt,
        string model = "gemini-2.5-pro",
        string effort = "medium",
        string? conversationId = null,
        List<RagChunkItem>? ragChunks = null,
        string? customAgyPath = null,
        CancellationToken cancellationToken = default)
    {
        var assistantMsg = new PersonalChatMessage
        {
            Role = "assistant",
            Model = model,
            ReasoningEffort = effort,
            InjectedRagChunks = ragChunks ?? new List<RagChunkItem>(),
            Timestamp = DateTime.UtcNow
        };

        var agyBinary = !string.IsNullOrEmpty(customAgyPath) && File.Exists(customAgyPath)
            ? customAgyPath
            : (OperatingSystem.IsWindows() ? "agy.exe" : "agy");

        // Prepare CLI arguments
        var argsBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            argsBuilder.Append($"--conversation \"{conversationId}\" ");
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            argsBuilder.Append($"--model \"{model}\" ");
        }

        if (!string.IsNullOrWhiteSpace(effort))
        {
            argsBuilder.Append($"--effort \"{effort}\" ");
        }

        if (profile.DangerouslySkipPermissions)
        {
            argsBuilder.Append("--dangerously-skip-permissions ");
        }

        argsBuilder.Append("--output-format json ");
        argsBuilder.Append("-p ");

        // Clean user prompt for command-line execution
        var escapedPrompt = prompt.Replace("\"", "\\\"");
        argsBuilder.Append($"\"{escapedPrompt}\"");

        var sw = Stopwatch.StartNew();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = agyBinary,
                Arguments = argsBuilder.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                WorkingDirectory = !string.IsNullOrWhiteSpace(profile.DefaultWorkspace) && Directory.Exists(profile.DefaultWorkspace)
                    ? profile.DefaultWorkspace
                    : Directory.GetCurrentDirectory()
            };

            // Sandbox Environment Isolation
            var targetHome = !string.IsNullOrEmpty(profile.CustomProfilePath)
                ? profile.CustomProfilePath
                : (profile.IsMainDefaultProfile()
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini-profiles", profile.Id));

            if (!profile.IsMainDefaultProfile())
            {
                Directory.CreateDirectory(targetHome);
                psi.EnvironmentVariables["USERPROFILE"] = targetHome;
                psi.EnvironmentVariables["HOME"] = targetHome;
            }

            // Keyring Decoupling
            psi.EnvironmentVariables["SSH_CONNECTION"] = "1";
            psi.EnvironmentVariables["SSH_CLIENT"] = "1";

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                assistantMsg.Content = "⚠️ Error: Unable to launch Antigravity CLI (agy) process.";
                return assistantMsg;
            }

            // Read output stream
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);
            await proc.WaitForExitAsync(cancellationToken);

            sw.Stop();
            assistantMsg.ExecutionDurationSeconds = sw.Elapsed.TotalSeconds;

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
            {
                ParseAgyJsonResponse(stdout, assistantMsg);
            }
            else
            {
                var errorText = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
                assistantMsg.Content = !string.IsNullOrWhiteSpace(errorText)
                    ? $"⚠️ Antigravity CLI Error (Exit Code {proc.ExitCode}):\n\n{errorText}"
                    : "⚠️ No response received from Antigravity CLI.";
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            assistantMsg.ExecutionDurationSeconds = sw.Elapsed.TotalSeconds;
            assistantMsg.Content = "⏹️ Generation stopped by user.";
        }
        catch (Exception ex)
        {
            sw.Stop();
            assistantMsg.ExecutionDurationSeconds = sw.Elapsed.TotalSeconds;
            assistantMsg.Content = $"⚠️ Exception executing agy CLI:\n\n{ex.Message}";
        }

        return assistantMsg;
    }

    private void ParseAgyJsonResponse(string rawOutput, PersonalChatMessage message)
    {
        try
        {
            var node = JsonNode.Parse(rawOutput);
            if (node == null)
            {
                message.Content = rawOutput;
                return;
            }

            var responseText = node["response"]?.GetValue<string>();
            message.Content = !string.IsNullOrEmpty(responseText) ? responseText.TrimEnd() : rawOutput;

            var duration = node["duration_seconds"]?.GetValue<double>() ?? 0;
            if (duration > 0) message.ExecutionDurationSeconds = duration;

            var usage = node["usage"];
            if (usage != null)
            {
                message.InputTokens = usage["input_tokens"]?.GetValue<int>() ?? 0;
                message.OutputTokens = usage["output_tokens"]?.GetValue<int>() ?? 0;
                message.ThinkingTokens = usage["thinking_tokens"]?.GetValue<int>() ?? 0;
                message.TotalTokens = usage["total_tokens"]?.GetValue<int>() ?? 0;
            }
        }
        catch
        {
            // If output is not JSON (e.g. plain text response), fall back to raw string
            message.Content = rawOutput.TrimEnd();
        }
    }

    public string GetRealtimeSwarmTaskStatusSummary(string workspaceRoot)
    {
        try
        {
            var swarmDir = Path.Combine(workspaceRoot, ".swarm");
            if (!Directory.Exists(swarmDir))
            {
                return "Swarm Status: Idle (No active swarm task directory found).";
            }

            var tasksFile = Path.Combine(swarmDir, "tasks.json");
            var busFile = Path.Combine(swarmDir, "bus.jsonl");

            var sb = new StringBuilder();

            // 1. Read tasks if available
            if (File.Exists(tasksFile))
            {
                var tasksJson = File.ReadAllText(tasksFile);
                var rootNode = JsonNode.Parse(tasksJson);
                if (rootNode is JsonArray arr)
                {
                    int total = arr.Count;
                    int completed = arr.Count(t => (t?["status"]?.GetValue<string>() ?? "").Equals("Completed", StringComparison.OrdinalIgnoreCase));
                    int inProgress = arr.Count(t => (t?["status"]?.GetValue<string>() ?? "").Equals("Executing", StringComparison.OrdinalIgnoreCase));

                    sb.AppendLine($"• Total Tasks: {total} | Completed: {completed} | In Progress: {inProgress}");

                    foreach (var item in arr)
                    {
                        var title = item?["title"]?.GetValue<string>() ?? "Untitled Task";
                        var status = item?["status"]?.GetValue<string>() ?? "Pending";
                        var worker = item?["assignedWorker"]?.GetValue<string>() ?? "Unassigned";
                        sb.AppendLine($"  - [{status}] {title} (Assigned: {worker})");
                    }
                }
            }

            // 2. Read latest bus event
            if (File.Exists(busFile))
            {
                var lines = File.ReadLines(busFile).Reverse().Take(3).ToList();
                if (lines.Count > 0)
                {
                    sb.AppendLine("• Recent Swarm Event Bus Activity:");
                    foreach (var line in lines)
                    {
                        try
                        {
                            var evt = JsonNode.Parse(line);
                            var sender = evt?["sender"]?.GetValue<string>() ?? "Worker";
                            var action = evt?["action"]?.GetValue<string>() ?? evt?["content"]?.GetValue<string>() ?? "";
                            if (!string.IsNullOrEmpty(action))
                            {
                                sb.AppendLine($"  - {sender}: {action}");
                            }
                        }
                        catch { }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "Swarm Status: Standby. All workers idle.";
        }
        catch (Exception ex)
        {
            return $"Swarm Status: Telemetry read note ({ex.Message})";
        }
    }
}
