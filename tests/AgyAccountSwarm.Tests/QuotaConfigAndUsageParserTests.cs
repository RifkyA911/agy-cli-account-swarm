using System;
using System.IO;
using System.Text.Json;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class QuotaConfigAndUsageParserTests
{
    [Fact]
    public void QuotaConfigFile_DefaultConfig_ContainsExpectedTiers()
    {
        var config = QuotaConfigService.CreateDefaultConfig();
        Assert.NotNull(config.Tiers);
        Assert.True(config.Tiers.ContainsKey("Basic"));
        Assert.True(config.Tiers.ContainsKey("Plus"));
        Assert.True(config.Tiers.ContainsKey("Pro"));
        Assert.True(config.Tiers.ContainsKey("Ultra"));

        Assert.Equal(100, config.Tiers["Basic"].DailyPrompts);
        Assert.Equal(300, config.Tiers["Plus"].DailyPrompts);
        Assert.Equal(1000, config.Tiers["Pro"].DailyPrompts);
        Assert.Equal(2500, config.Tiers["Ultra"].DailyPrompts);
    }

    [Fact]
    public void AgyUsageParser_ParsesAuthenticJsonStructureCorrectly()
    {
        // Authentic JSON verified directly against agy 1.2.12 CLI
        string sampleJson = """
        {
          "conversation_id": "",
          "status": "SUCCESS",
          "response": "Gemini Models\tWeekly Limit Remaining\t58%\t2026-10-02T02:01:21Z\nGemini Models\tFive Hour Limit Remaining\t96%\t2026-09-28T13:12:17Z\nClaude and GPT models\tWeekly Limit Remaining\t39%\t2026-10-02T01:07:40Z\nClaude and GPT models\tFive Hour Limit Remaining\t89%\t2026-09-28T13:11:39Z\n",
          "duration_seconds": 0,
          "num_turns": 0,
          "usage": {
            "input_tokens": 0,
            "output_tokens": 0,
            "thinking_tokens": 0,
            "cache_read_tokens": 0,
            "total_tokens": 0
          },
          "command": {
            "name": "usage",
            "data": {
              "description": "Within each group, models share a weekly limit and a 5-hour limit.",
              "groups": [
                {
                  "name": "Gemini Models",
                  "buckets": [
                    {
                      "id": "gemini-weekly",
                      "remaining_fraction": 0.5702829957008362,
                      "reset_time": "2026-10-02T02:01:21Z"
                    },
                    {
                      "id": "gemini-5h",
                      "remaining_fraction": 0.9590467214584351,
                      "reset_time": "2026-09-28T13:12:17Z"
                    }
                  ]
                },
                {
                  "name": "Claude and GPT models",
                  "buckets": [
                    {
                      "id": "3p-weekly",
                      "remaining_fraction": 0.38522493839263916,
                      "reset_time": "2026-10-02T01:07:40Z"
                    },
                    {
                      "id": "3p-5h",
                      "remaining_fraction": 0.885555624961853,
                      "reset_time": "2026-09-28T13:11:39Z"
                    }
                  ]
                }
              ]
            }
          }
        }
        """;

        var result = AgyUsageParser.Parse(sampleJson);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.GeminiWeeklyRemainingPercent);
        Assert.InRange(result.GeminiWeeklyRemainingPercent.Value, 57.0, 57.1);

        Assert.NotNull(result.Gemini5HourRemainingPercent);
        Assert.InRange(result.Gemini5HourRemainingPercent.Value, 95.9, 96.0);

        Assert.NotNull(result.ClaudeGptWeeklyRemainingPercent);
        Assert.InRange(result.ClaudeGptWeeklyRemainingPercent.Value, 38.5, 38.6);

        Assert.NotNull(result.ClaudeGpt5HourRemainingPercent);
        Assert.InRange(result.ClaudeGpt5HourRemainingPercent.Value, 88.5, 88.6);

        Assert.NotNull(result.GeminiWeeklyResetTime);
        Assert.NotNull(result.RawCliResponse);
        Assert.Contains("Gemini Models", result.RawCliResponse);
    }

    [Fact]
    public void AgyUsageParser_HandlesEmptyOrCorruptedJsonDefensively()
    {
        var emptyResult = AgyUsageParser.Parse("");
        Assert.False(emptyResult.IsSuccess);
        Assert.Null(emptyResult.GeminiWeeklyRemainingPercent);

        var corruptedResult = AgyUsageParser.Parse("{ not a valid json syntax ... ");
        Assert.False(corruptedResult.IsSuccess);
        Assert.Null(corruptedResult.GeminiWeeklyRemainingPercent);

        var missingGroupsResult = AgyUsageParser.Parse("""{"command": {"data": {}}}""");
        Assert.False(missingGroupsResult.IsSuccess);
        Assert.Null(missingGroupsResult.GeminiWeeklyRemainingPercent);
    }
}
