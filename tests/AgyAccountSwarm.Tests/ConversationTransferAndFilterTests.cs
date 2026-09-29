using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AgyAccountSwarm.Converters;
using AgyAccountSwarm.Models;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class ConversationTransferAndFilterTests : IDisposable
{
    private readonly string _testTempDir;

    public ConversationTransferAndFilterTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "AgyTransferTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task TransferConversations_CopiesHistoryAndFilesAccurately()
    {
        // Arrange
        var sourceDir = Path.Combine(_testTempDir, "SourceProfile");
        var targetDir = Path.Combine(_testTempDir, "TargetProfile");

        var sourceCli = Path.Combine(sourceDir, ".gemini", "antigravity-cli");
        var targetCli = Path.Combine(targetDir, ".gemini", "antigravity-cli");
        Directory.CreateDirectory(sourceCli);
        Directory.CreateDirectory(Path.Combine(sourceCli, "conversations"));
        Directory.CreateDirectory(Path.Combine(sourceCli, "brain", "conv-123"));

        // Setup source history.jsonl
        var histLine1 = "{\"conversationId\":\"conv-123\",\"timestamp\":1727500000000,\"display\":\"Refactor architecture\",\"workspace\":\"/workspace/app\"}";
        var histLine2 = "{\"conversationId\":\"conv-456\",\"timestamp\":1727510000000,\"display\":\"Add unit tests\",\"workspace\":\"/workspace/app\"}";
        File.WriteAllLines(Path.Combine(sourceCli, "history.jsonl"), new[] { histLine1, histLine2 });

        // Setup source conversation file & brain file
        File.WriteAllText(Path.Combine(sourceCli, "conversations", "conv-123.json"), "{\"turns\":[{\"user\":\"hi\"}]}");
        File.WriteAllText(Path.Combine(sourceCli, "brain", "conv-123", "transcript.jsonl"), "{\"step\":1}");

        var sourceProfile = new AccountProfile
        {
            Id = "src",
            Name = "Source Agent",
            CustomProfilePath = sourceDir
        };

        var targetProfile = new AccountProfile
        {
            Id = "tgt",
            Name = "Target Agent",
            CustomProfilePath = targetDir
        };

        var service = new ConversationTransferService();

        // Act - Scan source conversations
        var scanned = await service.GetConversationsAsync(sourceProfile);
        Assert.Equal(2, scanned.Count);
        Assert.Contains(scanned, c => c.ConversationId == "conv-123" && c.DisplayPrompt == "Refactor architecture");

        // Act - Transfer conv-123 only
        var transferResult = await service.TransferConversationsAsync(sourceProfile, targetProfile, new[] { "conv-123" });

        // Assert
        Assert.True(transferResult.Success);
        Assert.Equal(1, transferResult.TransferredCount);
        Assert.True(File.Exists(Path.Combine(targetCli, "history.jsonl")));
        Assert.True(File.Exists(Path.Combine(targetCli, "conversations", "conv-123.json")));
        Assert.True(File.Exists(Path.Combine(targetCli, "brain", "conv-123", "transcript.jsonl")));

        // Ensure conv-456 was NOT copied
        Assert.False(File.Exists(Path.Combine(targetCli, "conversations", "conv-456.json")));

        var targetHistoryLines = File.ReadAllLines(Path.Combine(targetCli, "history.jsonl"));
        Assert.Single(targetHistoryLines);
        Assert.Contains("conv-123", targetHistoryLines[0]);
    }

    [Fact]
    public async Task TransferConversations_PreventsDuplicateHistoryLines()
    {
        // Arrange
        var sourceDir = Path.Combine(_testTempDir, "SourceDup");
        var targetDir = Path.Combine(_testTempDir, "TargetDup");
        var sourceCli = Path.Combine(sourceDir, ".gemini", "antigravity-cli");
        var targetCli = Path.Combine(targetDir, ".gemini", "antigravity-cli");
        Directory.CreateDirectory(sourceCli);
        Directory.CreateDirectory(targetCli);

        var histLine = "{\"conversationId\":\"conv-dup\",\"timestamp\":1727500000000,\"display\":\"Duplicate test\"}";
        File.WriteAllLines(Path.Combine(sourceCli, "history.jsonl"), new[] { histLine });

        var sourceProfile = new AccountProfile { Id = "s", CustomProfilePath = sourceDir };
        var targetProfile = new AccountProfile { Id = "t", CustomProfilePath = targetDir };

        var service = new ConversationTransferService();

        // Transfer twice
        await service.TransferConversationsAsync(sourceProfile, targetProfile, new[] { "conv-dup" });
        await service.TransferConversationsAsync(sourceProfile, targetProfile, new[] { "conv-dup" }, overwrite: false);

        // Assert only 1 line in target history
        var lines = File.ReadAllLines(Path.Combine(targetCli, "history.jsonl"));
        Assert.Single(lines);
    }

    [Fact]
    public void BoolToFilterAccentConverter_ReturnsBlueWhenActive()
    {
        var converter = new BoolToFilterAccentConverter();
        var activeBrush = converter.Convert(true, typeof(System.Windows.Media.Brush), null, System.Globalization.CultureInfo.InvariantCulture);
        var inactiveBrush = converter.Convert(false, typeof(System.Windows.Media.Brush), null, System.Globalization.CultureInfo.InvariantCulture);

        Assert.NotNull(activeBrush);
        Assert.NotNull(inactiveBrush);
        Assert.NotEqual(activeBrush, inactiveBrush);
    }
}
