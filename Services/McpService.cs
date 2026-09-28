using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Models;

namespace AgyAccountSwarm.Services;

public interface IMcpService
{
    Task<List<McpServerConfig>> LoadMcpServersAsync();
    Task SaveMcpServerAsync(McpServerConfig server);
    Task DeleteMcpServerAsync(string serverName);
}

public class McpService : IMcpService
{
    public Task<List<McpServerConfig>> LoadMcpServersAsync()
    {
        return Task.Run(() =>
        {
            var servers = new List<McpServerConfig>();
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var mcpDir = Path.Combine(userHome, ".gemini", "antigravity-cli", "mcp");

            // 1. Discover local folder-based MCP servers
            if (Directory.Exists(mcpDir))
            {
                foreach (var dir in Directory.GetDirectories(mcpDir))
                {
                    var name = Path.GetFileName(dir);
                    var toolFiles = Directory.GetFiles(dir, "*.json")
                        .Select(f => Path.GetFileNameWithoutExtension(f))
                        .Where(f => !f.Equals("package", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var desc = "Local Model Context Protocol tool provider";
                    var instrFile = Path.Combine(dir, "instructions.md");
                    if (File.Exists(instrFile))
                    {
                        try
                        {
                            var lines = File.ReadAllLines(instrFile);
                            var first = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                            if (!string.IsNullOrEmpty(first)) desc = first.Trim('#', ' ');
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug($"[McpService] Could not read instructions.md in '{dir}': {ex.Message}");
                        }
                    }

                    servers.Add(new McpServerConfig
                    {
                        Name = name,
                        Command = "builtin / lazy",
                        Arguments = $"tools: {string.Join(", ", toolFiles)}",
                        Description = desc,
                        IsEnabled = true,
                        ToolsCount = toolFiles.Count,
                        Status = "Active (Lazy)",
                        Tools = toolFiles
                    });
                }
            }

            // 2. Discover from ~/.gemini/config/mcp_config.json if available
            var mcpConfigFile = Path.Combine(userHome, ".gemini", "config", "mcp_config.json");
            if (File.Exists(mcpConfigFile))
            {
                try
                {
                    var json = File.ReadAllText(mcpConfigFile);
                    if (!string.IsNullOrWhiteSpace(json) && json.Trim() != "{}")
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("mcpServers", out var serversElem))
                        {
                            foreach (var prop in serversElem.EnumerateObject())
                            {
                                if (servers.Any(s => s.Name.Equals(prop.Name, StringComparison.OrdinalIgnoreCase)))
                                    continue;

                                var cmd = prop.Value.TryGetProperty("command", out var c) ? c.GetString() ?? "" : "";
                                servers.Add(new McpServerConfig
                                {
                                    Name = prop.Name,
                                    Command = cmd,
                                    Description = "Configured in mcp_config.json",
                                    IsEnabled = true,
                                    ToolsCount = 1,
                                    Status = "Configured",
                                    Tools = [prop.Name]
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[McpService] Could not parse mcp_config.json: {ex.Message}");
                }
            }


            // If empty, supply standard community MCP server presets
            if (servers.Count == 0)
            {
                servers.Add(new McpServerConfig
                {
                    Name = "context7",
                    Command = "npx -y @upstash/context7-mcp",
                    Arguments = "--transport stdio",
                    Description = "Official Upstash Context7 documentation query engine",
                    IsEnabled = true,
                    ToolsCount = 2,
                    Status = "Active",
                    Tools = ["resolve-library-id", "query-docs"]
                });

                servers.Add(new McpServerConfig
                {
                    Name = "filesystem",
                    Command = "npx -y @modelcontextprotocol/server-filesystem",
                    Arguments = userHome,
                    Description = "Secure local directory read/write tool provider",
                    IsEnabled = true,
                    ToolsCount = 5,
                    Status = "Active",
                    Tools = ["read_file", "write_file", "list_dir", "search_files", "move_file"]
                });
            }

            Logger.Info($"[McpService] Discovered {servers.Count} MCP servers with {servers.Sum(s => s.ToolsCount)} total tools");
            return servers;
        });
    }

    public Task SaveMcpServerAsync(McpServerConfig server)
    {
        return Task.CompletedTask;
    }

    public Task DeleteMcpServerAsync(string serverName)
    {
        return Task.CompletedTask;
    }
}
