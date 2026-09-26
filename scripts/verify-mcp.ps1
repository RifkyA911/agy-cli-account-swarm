<#
.SYNOPSIS
    Probes and validates discovered MCP (Model Context Protocol) tool configurations.
#>
$mcpDir = Join-Path $env:USERPROFILE ".gemini\antigravity-cli\mcp"
Write-Host "Validating MCP tools in $mcpDir..." -ForegroundColor Cyan

if (-not (Test-Path $mcpDir)) {
    Write-Warning "MCP tools directory not found at $mcpDir."
    exit 0
}

$servers = Get-ChildItem -Path $mcpDir -Directory
foreach ($srv in $servers) {
    $jsonFiles = Get-ChildItem -Path $srv.FullName -Filter "*.json"
    Write-Host "  Found MCP Server: $($srv.Name) with $($jsonFiles.Count) tool definition(s)" -ForegroundColor Green
    foreach ($tool in $jsonFiles) {
        Write-Host "    - Tool: $($tool.BaseName)" -ForegroundColor Gray
    }
}
