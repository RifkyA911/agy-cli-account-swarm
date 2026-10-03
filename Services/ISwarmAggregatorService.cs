using System.Collections.Generic;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface ISwarmAggregatorService
{
    string EnsureProjectSwarmWorkspace(SwarmProject project, IEnumerable<AccountProfile> workers);

    string ResolveWorkerProjectDirectory(AccountProfile worker, SwarmProject project);

    Task PostMessageAsync(SwarmProject project, SwarmChatMessage message);

    Task<List<SwarmChatMessage>> LoadProjectMessagesAsync(SwarmProject project);

    Task<SwarmChatMessage> BroadcastUserInstructionAsync(SwarmProject project, string userText, string? targetWorker = null);

    Task PostAgentActionTelemetryAsync(SwarmProject project, string workerName, string role, string line, string? colorTag = null, string? avatarUrl = null, string? avatarInitial = null);

    Task UpdateBlackboardAsync(SwarmProject project, string updateContent, string author = "System");
}
