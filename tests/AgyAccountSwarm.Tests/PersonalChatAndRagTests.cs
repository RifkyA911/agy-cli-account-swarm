using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class PersonalChatAndRagTests : IDisposable
{
    private readonly string _tempDir;

    public PersonalChatAndRagTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AgyPersonalChatTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void PersonalChatMessage_CalculatesTelemetryAndFormattingAccurately()
    {
        var userMsg = new PersonalChatMessage
        {
            Role = "user",
            Content = "Explain the swarm bus architecture."
        };

        Assert.True(userMsg.IsUser);
        Assert.False(userMsg.IsAssistant);
        Assert.False(userMsg.IsSystem);
        Assert.Empty(userMsg.TokenTelemetryDisplay);

        var assistantMsg = new PersonalChatMessage
        {
            Role = "assistant",
            Content = "The swarm bus operates as an append-only JSONL stream.",
            Model = "gemini-2.5-pro",
            TotalTokens = 1420,
            ExecutionDurationSeconds = 2.45
        };

        Assert.False(assistantMsg.IsUser);
        Assert.True(assistantMsg.IsAssistant);
        Assert.Contains("1,420 tokens", assistantMsg.TokenTelemetryDisplay);
        Assert.Contains("2.5s", assistantMsg.TokenTelemetryDisplay);
        Assert.Contains("gemini-2.5-pro", assistantMsg.TokenTelemetryDisplay);
    }

    [Fact]
    public void PersonalChatSession_RelativeTimeDisplay_ReturnsAccuratePeriods()
    {
        var justNow = new PersonalChatSession { UpdatedAt = DateTime.UtcNow.AddSeconds(-20) };
        Assert.Equal("Just now", justNow.RelativeTimeDisplay);

        var minsAgo = new PersonalChatSession { UpdatedAt = DateTime.UtcNow.AddMinutes(-15) };
        Assert.Equal("15m ago", minsAgo.RelativeTimeDisplay);

        var hoursAgo = new PersonalChatSession { UpdatedAt = DateTime.UtcNow.AddHours(-4) };
        Assert.Equal("4h ago", hoursAgo.RelativeTimeDisplay);
    }

    [Fact]
    public void RagService_BuildAugmentedPrompt_InjectsContextAndQueryProperly()
    {
        var ragService = new RagService(_tempDir);
        var originalPrompt = "How do I run swarm aggregation?";

        // Case 1: No chunks and no live status context -> return verbatim original prompt
        var untouched = ragService.BuildAugmentedPrompt(originalPrompt, new List<RagChunkItem>(), null);
        Assert.Equal(originalPrompt, untouched);

        // Case 2: Injected RAG chunks + Live Swarm Task Context
        var chunks = new List<RagChunkItem>
        {
            new()
            {
                ChunkId = "chunk-101",
                KbName = "agy-swarm",
                Snippet = "SwarmAggregator monitors .swarm/bus.jsonl using FileSystemWatcher.",
                Score = 0.92
            }
        };

        var liveStatus = "• Total Tasks: 3 | Completed: 2 | In Progress: 1";

        var augmented = ragService.BuildAugmentedPrompt(originalPrompt, chunks, liveStatus);

        Assert.Contains("=== KNOWLEDGE BASE RELEVANT CONTEXT (RAG) ===", augmented);
        Assert.Contains("SwarmAggregator monitors .swarm/bus.jsonl", augmented);
        Assert.Contains("Chunk #chunk-101", augmented);
        Assert.Contains("=== REAL-TIME SWARM STATUS & WORKER TELEMETRY ===", augmented);
        Assert.Contains("Total Tasks: 3", augmented);
        Assert.Contains("=== USER QUERY ===", augmented);
        Assert.Contains(originalPrompt, augmented);
        Assert.Contains("Instructions:", augmented);
    }

    [Fact]
    public async Task PersonalChatService_SessionPersistence_SavesAndLoadsSessionsHermetically()
    {
        var chatService = new PersonalChatService(_tempDir);
        var profileId = "account-bravo-123";

        // Initial sessions for fresh profile -> generates 1 default session
        var initialSessions = await chatService.GetSessionsAsync(profileId);
        Assert.Single(initialSessions);
        var activeSession = initialSessions[0];

        // Add mock messages
        var messages = new List<PersonalChatMessage>
        {
            new() { Role = "user", Content = "Halo AGY, jelaskan arsitektur worker swarm." },
            new() { Role = "assistant", Content = "Worker swarm memanfaatkan multi-process sandbox terisolasi.", TotalTokens = 520, ExecutionDurationSeconds = 1.2 }
        };

        activeSession.Title = "Diskusi Worker Swarm";
        await chatService.SaveSessionAsync(activeSession, messages);

        // Verify loaded messages
        var loadedMessages = await chatService.LoadMessagesAsync(profileId, activeSession.Id);
        Assert.Equal(2, loadedMessages.Count);
        Assert.Equal("Halo AGY, jelaskan arsitektur worker swarm.", loadedMessages[0].Content);
        Assert.Equal(520, loadedMessages[1].TotalTokens);

        // Verify loaded sessions index
        var loadedSessions = await chatService.GetSessionsAsync(profileId);
        Assert.Single(loadedSessions);
        Assert.Equal("Diskusi Worker Swarm", loadedSessions[0].Title);
        Assert.Equal(1, loadedSessions[0].TurnCount);
        Assert.Equal("Halo AGY, jelaskan arsitektur worker swarm.", loadedSessions[0].LastQuerySnippet);

        // Delete session
        await chatService.DeleteSessionAsync(profileId, activeSession.Id);

        // After deletion, GetSessionsAsync should return empty or generate a new fresh session
        var remainingAfterDelete = await chatService.GetSessionsAsync(profileId);
        Assert.DoesNotContain(remainingAfterDelete, s => s.Id == activeSession.Id);
    }

    [Fact]
    public void PersonalChatService_GetRealtimeSwarmTaskStatusSummary_ParsesTasksAndBusEvents()
    {
        var chatService = new PersonalChatService(_tempDir);

        // Case A: No .swarm directory
        var idleSummary = chatService.GetRealtimeSwarmTaskStatusSummary(_tempDir);
        Assert.Contains("Idle", idleSummary);

        // Case B: Create .swarm directory with tasks.json and bus.jsonl
        var swarmDir = Path.Combine(_tempDir, ".swarm");
        Directory.CreateDirectory(swarmDir);

        var tasks = new[]
        {
            new { id = "task-1", title = "Setup DB schema", status = "Completed", assignedWorker = "Worker-Alpha" },
            new { id = "task-2", title = "Build Avalonia UI", status = "Executing", assignedWorker = "Worker-Beta" },
            new { id = "task-3", title = "Write Unit Tests", status = "Pending", assignedWorker = "Unassigned" }
        };

        File.WriteAllText(Path.Combine(swarmDir, "tasks.json"), JsonSerializer.Serialize(tasks));

        var busLines = new[]
        {
            JsonSerializer.Serialize(new { sender = "Worker-Alpha", action = "Completed task-1 schema creation" }),
            JsonSerializer.Serialize(new { sender = "Worker-Beta", action = "Compiling Avalonia UI layouts" })
        };
        File.WriteAllLines(Path.Combine(swarmDir, "bus.jsonl"), busLines);

        var activeSummary = chatService.GetRealtimeSwarmTaskStatusSummary(_tempDir);

        Assert.Contains("Total Tasks: 3", activeSummary);
        Assert.Contains("Completed: 1", activeSummary);
        Assert.Contains("In Progress: 1", activeSummary);
        Assert.Contains("[Completed] Setup DB schema", activeSummary);
        Assert.Contains("[Executing] Build Avalonia UI", activeSummary);
        Assert.Contains("Recent Swarm Event Bus Activity", activeSummary);
        Assert.Contains("Worker-Beta: Compiling Avalonia UI layouts", activeSummary);
    }
}
