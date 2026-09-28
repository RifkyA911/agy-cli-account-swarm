using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgyAccountSwarm.Services;

public class TierAllowance
{
    public int DailyPrompts { get; set; }
    public int WeeklyPrompts { get; set; }
    public long DailyTokens { get; set; }
}

public class QuotaConfigFile
{
    public Dictionary<string, TierAllowance> Tiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public interface IQuotaConfigService
{
    int GetDailyQuota(string? tier);
    int GetWeeklyQuota(string? tier);
    long GetDailyTokensLimit(string? tier);
    Task LoadConfigAsync();
}

public class QuotaConfigService : IQuotaConfigService
{
    private readonly string _configFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private QuotaConfigFile _config = CreateDefaultConfig();

    public QuotaConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AgyAccountSwarm");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _configFilePath = Path.Combine(dir, "quota_config.json");
        LoadConfigSync();
    }

    public static QuotaConfigFile CreateDefaultConfig()
    {
        return new QuotaConfigFile
        {
            Tiers = new Dictionary<string, TierAllowance>(StringComparer.OrdinalIgnoreCase)
            {
                ["Basic"] = new() { DailyPrompts = 100, WeeklyPrompts = 500, DailyTokens = 500000L },
                ["Free"] = new() { DailyPrompts = 100, WeeklyPrompts = 500, DailyTokens = 500000L },
                ["Unverified"] = new() { DailyPrompts = 100, WeeklyPrompts = 500, DailyTokens = 500000L },
                ["Plus"] = new() { DailyPrompts = 300, WeeklyPrompts = 1500, DailyTokens = 1500000L },
                ["Pro"] = new() { DailyPrompts = 1000, WeeklyPrompts = 5000, DailyTokens = 5000000L },
                ["Ultra"] = new() { DailyPrompts = 2500, WeeklyPrompts = 12500, DailyTokens = 15000000L },
                ["Enterprise"] = new() { DailyPrompts = 2500, WeeklyPrompts = 12500, DailyTokens = 15000000L }
            }
        };
    }

    private void LoadConfigSync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var loaded = JsonSerializer.Deserialize<QuotaConfigFile>(json, JsonOptions);
                if (loaded?.Tiers != null && loaded.Tiers.Count > 0)
                {
                    _config = loaded;
                    return;
                }
            }

            // Generate default config file if missing
            _config = CreateDefaultConfig();
            var serialized = JsonSerializer.Serialize(_config, JsonOptions);
            File.WriteAllText(_configFilePath, serialized);
            Logger.Info($"[QuotaConfig] Initialized configurable quota table at '{_configFilePath}'");
        }
        catch (Exception ex)
        {
            Logger.Warn($"[QuotaConfig] Failed reading '{_configFilePath}', using defaults: {ex.Message}");
            _config = CreateDefaultConfig();
        }
    }

    public async Task LoadConfigAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var loaded = JsonSerializer.Deserialize<QuotaConfigFile>(json, JsonOptions);
                if (loaded?.Tiers != null && loaded.Tiers.Count > 0)
                {
                    _config = loaded;
                    return;
                }
            }
            _config = CreateDefaultConfig();
            var serialized = JsonSerializer.Serialize(_config, JsonOptions);
            await File.WriteAllTextAsync(_configFilePath, serialized);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[QuotaConfig] Failed reading '{_configFilePath}' asynchronously, using defaults: {ex.Message}");
            _config = CreateDefaultConfig();
        }
    }

    public int GetDailyQuota(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier)) return 1000;
        if (_config.Tiers.TryGetValue(tier.Trim(), out var allowance) && allowance.DailyPrompts > 0)
        {
            return allowance.DailyPrompts;
        }
        return 1000; // Pro default
    }

    public int GetWeeklyQuota(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier)) return 5000;
        if (_config.Tiers.TryGetValue(tier.Trim(), out var allowance) && allowance.WeeklyPrompts > 0)
        {
            return allowance.WeeklyPrompts;
        }
        return 5000; // Pro default
    }

    public long GetDailyTokensLimit(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier)) return 5000000L;
        if (_config.Tiers.TryGetValue(tier.Trim(), out var allowance) && allowance.DailyTokens > 0)
        {
            return allowance.DailyTokens;
        }
        return 5000000L; // Pro default
    }
}
