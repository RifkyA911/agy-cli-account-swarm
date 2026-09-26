using System;
using System.Collections.Generic;

namespace AgyAccountSwarm.Models;

public class McpServerConfig
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int ToolsCount { get; set; } = 0;
    public string Status { get; set; } = "Active";
    public List<string> Tools { get; set; } = [];
}
