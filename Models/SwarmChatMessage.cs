using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgyAccountSwarm.Models;

public enum SwarmMessageType
{
    UserBroadcast,   // Sent by the user to the swarm or specific worker
    AgentMessage,    // Direct communication from an agent/worker
    AgentAction,     // Tool call, file write, or test command
    SystemEvent,     // Swarm lifecycle event (dispatch, worktree created, consensus reached)
    Handoff          // Agent handing off task/API to another agent
}

public partial class SwarmChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _projectId = string.Empty;

    [ObservableProperty]
    private string _senderName = "System";

    [ObservableProperty]
    private string _senderRole = "Orchestrator";

    [ObservableProperty]
    private string _senderColor = "#10B981";

    [ObservableProperty]
    private string? _avatarUrl;

    [ObservableProperty]
    private string _avatarInitial = "S";

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private SwarmMessageType _type = SwarmMessageType.SystemEvent;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.UtcNow;

    [ObservableProperty]
    private string? _targetWorker; // null for broadcast to all

    public string TimestampFormatted => Timestamp.ToLocalTime().ToString("HH:mm:ss");

    public bool IsUser => Type == SwarmMessageType.UserBroadcast;
    public bool IsSystem => Type == SwarmMessageType.SystemEvent;
    public bool IsAction => Type == SwarmMessageType.AgentAction;
}
