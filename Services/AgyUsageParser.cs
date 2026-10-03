using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgyAccountSwarm.Services;

public class AgyUsageResult
{
    public bool IsSuccess { get; set; }
    public double? GeminiWeeklyRemainingPercent { get; set; }
    public string? GeminiWeeklyRefreshesIn { get; set; }
    public DateTime? GeminiWeeklyResetTime { get; set; }

    public double? Gemini5HourRemainingPercent { get; set; }
    public string? Gemini5HourRefreshesIn { get; set; }
    public DateTime? Gemini5HourResetTime { get; set; }

    public double? ClaudeGptWeeklyRemainingPercent { get; set; }
    public string? ClaudeGptWeeklyRefreshesIn { get; set; }
    public DateTime? ClaudeGptWeeklyResetTime { get; set; }

    public double? ClaudeGpt5HourRemainingPercent { get; set; }
    public string? ClaudeGpt5HourRefreshesIn { get; set; }
    public DateTime? ClaudeGpt5HourResetTime { get; set; }

    public string? RawCliResponse { get; set; }
}

/// <summary>
/// Defensive parser and background fetcher for Antigravity CLI authentic `/usage` command outputs.
/// Extracts remaining fractions and UTC refresh timestamps for Gemini and third-party models with zero turn/token consumption.
/// </summary>
public static class AgyUsageParser
{
    /// <summary>
    /// Parses a JSON output string produced by <c>agy -p "/usage" --output-format json</c>.
    /// </summary>
    /// <param name="json">Raw JSON response string.</param>
    /// <returns>A populated <see cref="AgyUsageResult"/> with parsed model limits and refresh times.</returns>
    public static AgyUsageResult Parse(string json)
    {
        var result = new AgyUsageResult();
        if (string.IsNullOrWhiteSpace(json)) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var respProp) && respProp.GetString() is { Length: > 0 } respText)
            {
                result.RawCliResponse = respText;
            }

            if (!root.TryGetProperty("command", out var cmdProp) ||
                !cmdProp.TryGetProperty("data", out var dataProp) ||
                !dataProp.TryGetProperty("groups", out var groupsProp) ||
                groupsProp.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var group in groupsProp.EnumerateArray())
            {
                if (!group.TryGetProperty("buckets", out var bucketsProp) ||
                    bucketsProp.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var bucket in bucketsProp.EnumerateArray())
                {
                    string? id = bucket.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                    if (string.IsNullOrEmpty(id)) continue;

                    double? fraction = null;
                    if (bucket.TryGetProperty("remaining_fraction", out var fracProp) && fracProp.TryGetDouble(out var fracVal))
                    {
                        fraction = fracVal;
                    }

                    string? resetTimeStr = bucket.TryGetProperty("reset_time", out var rtProp) ? rtProp.GetString() : null;
                    DateTime? resetUtc = null;
                    string? refreshesIn = null;

                    if (!string.IsNullOrEmpty(resetTimeStr) &&
                        DateTime.TryParse(resetTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                    {
                        resetUtc = dt;
                        var diff = dt - DateTime.UtcNow;
                        if (diff > TimeSpan.Zero)
                        {
                            if (diff.TotalDays >= 1)
                            {
                                refreshesIn = $"{(int)diff.TotalDays}d {diff.Hours}h";
                            }
                            else if (diff.TotalHours >= 1)
                            {
                                refreshesIn = $"{(int)diff.TotalHours}h {diff.Minutes}m";
                            }
                            else
                            {
                                refreshesIn = $"{Math.Max(1, (int)diff.TotalMinutes)}m";
                            }
                        }
                        else
                        {
                            refreshesIn = "Refreshing now";
                        }
                    }

                    double percent = fraction.HasValue ? Math.Clamp(fraction.Value * 100.0, 0.0, 100.0) : 100.0;

                    switch (id.ToLowerInvariant())
                    {
                        case "gemini-weekly":
                            result.GeminiWeeklyRemainingPercent = percent;
                            result.GeminiWeeklyRefreshesIn = refreshesIn;
                            result.GeminiWeeklyResetTime = resetUtc;
                            break;

                        case "gemini-5h":
                            result.Gemini5HourRemainingPercent = percent;
                            result.Gemini5HourRefreshesIn = refreshesIn;
                            result.Gemini5HourResetTime = resetUtc;
                            break;

                        case "3p-weekly":
                            result.ClaudeGptWeeklyRemainingPercent = percent;
                            result.ClaudeGptWeeklyRefreshesIn = refreshesIn;
                            result.ClaudeGptWeeklyResetTime = resetUtc;
                            break;

                        case "3p-5h":
                            result.ClaudeGpt5HourRemainingPercent = percent;
                            result.ClaudeGpt5HourRefreshesIn = refreshesIn;
                            result.ClaudeGpt5HourResetTime = resetUtc;
                            break;
                    }
                }
            }

            result.IsSuccess = result.GeminiWeeklyRemainingPercent.HasValue || result.ClaudeGptWeeklyRemainingPercent.HasValue;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[AgyUsageParser] Failed parsing CLI usage JSON: {ex.Message}");
        }

        return result;
    }

    private static readonly System.Threading.SemaphoreSlim _cliLock = new(1, 1);

    /// <summary>
    /// Executes <c>agy -p "/usage" --output-format json</c> in the specified sandbox directory
    /// using a serialized mutex lock to prevent concurrent subprocess collisions.
    /// </summary>
    /// <param name="effectiveProfileDir">Target profile root directory.</param>
    /// <param name="isIsolated">Whether this is a secondary isolated profile requiring keyring decoupling.</param>
    /// <param name="agyExecutablePath">Absolute path to the agy executable binary.</param>
    /// <returns>Parsed <see cref="AgyUsageResult"/>, or <c>null</c> if invocation failed.</returns>
    public static async Task<AgyUsageResult?> FetchUsageAsync(string effectiveProfileDir, bool isIsolated, string? agyExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(agyExecutablePath) || !File.Exists(agyExecutablePath))
        {
            return null;
        }

        await _cliLock.WaitAsync();
        try
        {
            var workingDir = Directory.Exists(effectiveProfileDir) ? effectiveProfileDir : Path.GetTempPath();

            var isWindowsBatch = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) &&
                                 (agyExecutablePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                                  agyExecutablePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase));

            var psi = new ProcessStartInfo
            {
                FileName = isWindowsBatch ? "cmd.exe" : agyExecutablePath,
                Arguments = isWindowsBatch
                    ? $"/c \"\"{agyExecutablePath}\" -p \"/usage\" --output-format json\""
                    : "-p \"/usage\" --output-format json",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            psi.Environment["USERPROFILE"] = effectiveProfileDir;
            psi.Environment["HOME"] = effectiveProfileDir;
            psi.Environment["ANTIGRAVITY_APP_DATA_DIR"] = Path.Combine(effectiveProfileDir, ".gemini", "antigravity-cli");
            psi.Environment["JETSKI_APP_DATA_DIR"] = Path.Combine(effectiveProfileDir, ".gemini", "antigravity-cli");
            psi.Environment["WT_SESSION"] = "";
            psi.Environment["CI"] = "1";
            psi.Environment["TERM"] = "dumb";
            psi.Environment["NO_COLOR"] = "1";
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["GIT_ASKPASS"] = "";
            psi.Environment["SSH_ASKPASS"] = "";

            if (isIsolated)
            {
                psi.Environment["SSH_CONNECTION"] = "1";
                psi.Environment["SSH_CLIENT"] = "1";
            }

            using var process = new Process { StartInfo = psi };
            process.Start();
            process.StandardInput.Close();

            var readOutTask = process.StandardOutput.ReadToEndAsync();
            var readErrTask = process.StandardError.ReadToEndAsync();
            var timeoutTask = Task.Delay(10000); // 10 second safety timeout

            var finished = await Task.WhenAny(Task.WhenAll(readOutTask, readErrTask), timeoutTask);
            if (finished != timeoutTask && readOutTask.IsCompleted)
            {
                var output = await readOutTask;
                if (!string.IsNullOrWhiteSpace(output) && output.TrimStart().StartsWith("{"))
                {
                    var parsed = Parse(output);
                    if (parsed.IsSuccess)
                    {
                        Logger.Info($"[AgyUsageParser] Successfully fetched live authentic usage from agy CLI for profile at '{effectiveProfileDir}'");
                        return parsed;
                    }
                }
            }
            else
            {
                try { process.Kill(); } catch { }
                Logger.Warn($"[AgyUsageParser] Timed out waiting for 'agy -p /usage' at '{effectiveProfileDir}'");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"[AgyUsageParser] Error executing 'agy -p /usage': {ex.Message}");
        }
        finally
        {
            _cliLock.Release();
        }

        return null;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime CachedAt, AgyUsageResult Result)> _usageCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Retrieves cached usage data or fetches fresh data if allowed.
    /// During automatic background sync, <paramref name="allowCliSpawn"/> is false to avoid CLI flickers.
    /// </summary>
    public static async Task<AgyUsageResult?> FetchUsageCachedAsync(
        string effectiveProfileDir,
        bool isIsolated,
        string? agyExecutablePath,
        TimeSpan? maxAge = null,
        bool allowCliSpawn = true)
    {
        var ttl = maxAge ?? TimeSpan.FromMinutes(30);
        if (_usageCache.TryGetValue(effectiveProfileDir, out var entry) && (DateTime.UtcNow - entry.CachedAt) < ttl)
        {
            return entry.Result;
        }

        if (!allowCliSpawn)
        {
            // Fall back to stale cache if available, but never spawn a process during background auto-sync
            return _usageCache.TryGetValue(effectiveProfileDir, out var stale) ? stale.Result : null;
        }

        var result = await FetchUsageAsync(effectiveProfileDir, isIsolated, agyExecutablePath);
        if (result != null && result.IsSuccess)
        {
            _usageCache[effectiveProfileDir] = (DateTime.UtcNow, result);
        }
        return result;
    }

    public static void InvalidateCache(string? profileDir = null)
    {
        if (string.IsNullOrEmpty(profileDir))
        {
            _usageCache.Clear();
        }
        else
        {
            _usageCache.TryRemove(profileDir, out _);
        }
    }
}

