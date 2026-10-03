using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class SwarmAggregatorTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly SwarmAggregatorService _aggregator;

    public SwarmAggregatorTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), "SwarmAggregatorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempTestDir);
        _aggregator = new SwarmAggregatorService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void EnsureProjectSwarmWorkspace_CreatesStructureAndFiles()
    {
        var project = new SwarmProject
        {
            Id = "proj-test",
            Name = "Quantum Simulator",
            Description = "Quantum computing algorithms",
            TechStack = "Python / FastAPI",
            RootDirectory = _tempTestDir
        };

        var workers = new List<AccountProfile>
        {
            new() { Id = "w1", Name = "Agent-Alpha", Tier = "Pro", PreferredModel = "gemini-2.5-flash", ColorTag = "#3B82F6" },
            new() { Id = "w2", Name = "Agent-Beta", Tier = "Ultra", PreferredModel = "gemini-2.5-pro", ColorTag = "#10B981" }
        };

        _aggregator.EnsureProjectSwarmWorkspace(project, workers);

        var swarmDir = Path.Combine(_tempTestDir, ".swarm");
        Assert.True(Directory.Exists(swarmDir));

        var manifestPath = Path.Combine(swarmDir, "manifest.json");
        Assert.True(File.Exists(manifestPath));
        var manifestContent = File.ReadAllText(manifestPath);
        Assert.Contains("Quantum Simulator", manifestContent);
        Assert.Contains("Python / FastAPI", manifestContent);
        Assert.Contains("Agent-Alpha", manifestContent);
        Assert.Contains("Agent-Beta", manifestContent);

        var busPath = Path.Combine(swarmDir, "bus.jsonl");
        Assert.True(File.Exists(busPath));
        var busContent = File.ReadAllText(busPath);
        Assert.Contains("Quantum Simulator", busContent);
        Assert.Contains("initialized", busContent);

        var blackboardPath = Path.Combine(swarmDir, "blackboard.md");
        Assert.True(File.Exists(blackboardPath));
        var blackboardContent = File.ReadAllText(blackboardPath);
        Assert.Contains("# Swarm Project Blackboard: Quantum Simulator", blackboardContent);
        Assert.Contains("Active Worker Matrix", blackboardContent);
    }

    [Fact]
    public void ResolveWorkerProjectDirectory_CreatesSanitizedDedicatedWorkspace()
    {
        var workerProfile = new AccountProfile
        {
            Id = "worker-1",
            Name = "DataWorker",
            DefaultWorkspace = Path.Combine(_tempTestDir, "profiles", "worker-1", "workspace")
        };

        var project = new SwarmProject
        {
            Name = "Alpha-Omega Payment Engine"
        };

        var resolvedPath = _aggregator.ResolveWorkerProjectDirectory(workerProfile, project);

        Assert.True(Directory.Exists(resolvedPath));
        Assert.EndsWith("alpha-omega-payment-engine", resolvedPath);
    }

    [Fact]
    public async Task PostMessage_And_LoadProjectMessages_RoundtripsAccurately()
    {
        var project = new SwarmProject
        {
            Id = "proj-msg-test",
            Name = "Chat Test",
            RootDirectory = _tempTestDir
        };

        // Post a user broadcast
        await _aggregator.BroadcastUserInstructionAsync(project, "Ensure all unit tests cover edge cases.", targetWorker: "Agent-1");

        // Post an agent action
        await _aggregator.PostAgentActionTelemetryAsync(project, "Agent-2", "QA", line: "dotnet test: 42 passed", colorTag: "#F59E0B");

        // Post a standard agent message
        await _aggregator.PostMessageAsync(project, new SwarmChatMessage
        {
            ProjectId = project.Id,
            SenderName = "Agent-1",
            SenderRole = "Backend",
            SenderColor = "#3B82F6",
            Content = "Added validation logic for edge cases.",
            Type = SwarmMessageType.AgentMessage
        });

        var messages = (await _aggregator.LoadProjectMessagesAsync(project)).ToList();

        Assert.Equal(3, messages.Count);

        // Message 1: User Broadcast
        Assert.Equal(SwarmMessageType.UserBroadcast, messages[0].Type);
        Assert.Equal("You (User)", messages[0].SenderName);
        Assert.Equal("Agent-1", messages[0].TargetWorker);
        Assert.Equal("Ensure all unit tests cover edge cases.", messages[0].Content);

        // Message 2: Agent Action
        Assert.Equal(SwarmMessageType.AgentAction, messages[1].Type);
        Assert.Equal("Agent-2", messages[1].SenderName);
        Assert.Equal("QA", messages[1].SenderRole);
        Assert.Contains("42 passed", messages[1].Content);

        // Message 3: Agent Message
        Assert.Equal(SwarmMessageType.AgentMessage, messages[2].Type);
        Assert.Equal("Agent-1", messages[2].SenderName);
        Assert.Equal("Backend", messages[2].SenderRole);
        Assert.Equal("Added validation logic for edge cases.", messages[2].Content);
    }

    [Fact]
    public async Task UpdateBlackboardAsync_AppendsSection()
    {
        var project = new SwarmProject
        {
            Id = "proj-blackboard-test",
            Name = "Blackboard Test",
            RootDirectory = _tempTestDir
        };

        _aggregator.EnsureProjectSwarmWorkspace(project, new List<AccountProfile>());

        await _aggregator.UpdateBlackboardAsync(project, "GET /api/v1/health -> 200 OK\nPOST /api/v1/auth -> JWT", author: "Architect");

        var blackboardPath = Path.Combine(_tempTestDir, ".swarm", "blackboard.md");
        Assert.True(File.Exists(blackboardPath));

        var content = await File.ReadAllTextAsync(blackboardPath);
        Assert.Contains("**Architect**", content);
        Assert.Contains("GET /api/v1/health", content);
        Assert.Contains("POST /api/v1/auth", content);
    }

    [Fact]
    public void FleetDispatcherService_SynthesizePrompt_InjectsSwarmBusAwareness()
    {
        var dispatcher = new FleetDispatcherService(swarmAggregatorService: _aggregator);

        var project = new SwarmProject
        {
            Name = "Real-Time Notification Core",
            TechStack = "C# / .NET 9",
            Description = "Distributed pub-sub microservices"
        };

        var participatingWorkers = new List<AccountProfile>
        {
            new() { Name = "BackendAgent", Description = "API Developer", PreferredModel = "gemini-2.5-pro" },
            new() { Name = "FrontendAgent", Description = "UI Developer", PreferredModel = "gemini-2.5-flash" }
        };

        var prompt = dispatcher.SynthesizePrompt(
            "Build real-time notification service",
            "Backend",
            DispatchMode.Broadcast,
            workerIndex: 0,
            totalWorkers: 2,
            project: project,
            allParticipatingWorkers: participatingWorkers);

        Assert.Contains("Build real-time notification service", prompt);
        Assert.Contains("[INTER-CLI COMMUNICATION & BLACKBOARD]", prompt);
        Assert.Contains(".swarm/blackboard.md", prompt);
        Assert.Contains(".swarm/bus.jsonl", prompt);
        Assert.Contains("BackendAgent", prompt);
        Assert.Contains("FrontendAgent", prompt);
    }
}
