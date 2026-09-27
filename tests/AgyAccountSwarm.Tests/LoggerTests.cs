using System;
using System.Linq;
using AgyAccountSwarm.Services;
using Xunit;

namespace AgyAccountSwarm.Tests;

public class LoggerTests
{
    [Fact]
    public void Logger_StoresEntriesInRecentBuffer()
    {
        // Arrange
        Logger.Clear();

        // Act
        Logger.Info("Test info message for swarm");
        Logger.Debug("Test debug message");
        Logger.Warning("Test warning message");

        // Assert
        var recent = Logger.GetRecentLogLines();
        Assert.True(recent.Count >= 3);
        Assert.Contains(recent, l => l.Contains("[INFO]") && l.Contains("Test info message for swarm"));
        Assert.Contains(recent, l => l.Contains("[DEBUG]") && l.Contains("Test debug message"));
        Assert.Contains(recent, l => l.Contains("[WARN]") && l.Contains("Test warning message"));
    }

    [Fact]
    public void Logger_Clear_EmptiesRecentBuffer()
    {
        // Arrange
        Logger.Info("Message before clear");
        Assert.NotEmpty(Logger.GetRecentLogLines());

        // Act
        Logger.Clear();

        // Assert
        var remaining = Logger.GetRecentLogLines();
        Assert.Empty(remaining);
    }

    [Fact]
    public void Logger_RingBuffer_CapsAtMaxCapacity()
    {
        // Arrange
        Logger.Clear();

        // Act: Log 1,200 entries (cap is 1,000)
        for (int i = 0; i < 1200; i++)
        {
            Logger.Debug($"Bulk trace test entry #{i}");
        }

        // Assert
        var lines = Logger.GetRecentLogLines();
        Assert.Equal(1000, lines.Count);
        // Oldest entries (e.g. #0 to #199) should have rolled off
        Assert.DoesNotContain(lines, l => l.Contains("Bulk trace test entry #0\r") || l.EndsWith("#0"));
        Assert.Contains(lines, l => l.Contains("Bulk trace test entry #1199"));
    }
}
