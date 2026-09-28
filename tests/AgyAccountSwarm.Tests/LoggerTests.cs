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

    [Fact]
    public void LogExcelExportService_ParseLogLine_ParsesFieldsAccurately()
    {
        var rawLine = "[2026-09-28 21:00:15.123] [ERROR] [ProfileDoctor] Failed to read token file <critical>";
        var item = LogExcelExportService.ParseLogLine(rawLine);

        Assert.Equal("2026-09-28 21:00:15.123", item.Timestamp);
        Assert.Equal("ERROR", item.Level);
        Assert.Equal("ProfileDoctor", item.Source);
        Assert.Equal("Failed to read token file <critical>", item.Message);
    }

    [Fact]
    public void LogExcelExportService_GeneratesValidZipAndWorksheet_WhenPopulated()
    {
        var logs = new[]
        {
            "[2026-09-28 20:00:00.000] [INFO] [App] Agy CLI Account Swarm started",
            "[2026-09-28 20:01:00.000] [WARN] [AuthDetector] Token expiring soon & need refresh",
            "[2026-09-28 20:02:00.000] [ERROR] [TerminalLauncher] Error launching <test> \"quoted\""
        };

        var bytes = LogExcelExportService.GenerateExcelWorkbook(logs);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        // Verify valid OpenXML OPC zip archive
        using var stream = new System.IO.MemoryStream(bytes);
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

        var contentTypes = zip.GetEntry("[Content_Types].xml");
        var workbook = zip.GetEntry("xl/workbook.xml");
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        var styles = zip.GetEntry("xl/styles.xml");

        Assert.NotNull(contentTypes);
        Assert.NotNull(workbook);
        Assert.NotNull(sheet);
        Assert.NotNull(styles);

        // Read sheet xml and verify content & XML escaping
        using var reader = new System.IO.StreamReader(sheet.Open());
        var sheetXml = reader.ReadToEnd();

        Assert.Contains("Timestamp (UTC/Local)", sheetXml);
        Assert.Contains("Log Level", sheetXml);
        Assert.Contains("Component / Source", sheetXml);
        Assert.Contains("Message Details", sheetXml);
        Assert.Contains("Agy CLI Account Swarm started", sheetXml);
        Assert.Contains("&amp; need refresh", sheetXml); // Escaped &
        Assert.Contains("&lt;test&gt;", sheetXml); // Escaped < and >
    }

    [Fact]
    public void LogExcelExportService_HandlesEmptyLogsGracefully()
    {
        var emptyLogs = Array.Empty<string>();
        var bytes = LogExcelExportService.GenerateExcelWorkbook(emptyLogs);
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        using var stream = new System.IO.MemoryStream(bytes);
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);

        using var reader = new System.IO.StreamReader(sheet.Open());
        var sheetXml = reader.ReadToEnd();
        Assert.Contains("Timestamp (UTC/Local)", sheetXml);
    }
}
