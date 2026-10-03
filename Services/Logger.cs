using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AgyAccountSwarm.Services;

public static class Logger
{
    private static readonly object LockObj = new();
    private static readonly string LogDirPath;
    private static readonly Queue<string> RecentBuffer = new(1000);
    private const int MaxBufferSize = 1000;

    private static readonly System.Text.RegularExpressions.Regex TokenRegex = new(
        @"(eyJ[a-zA-Z0-9_\-]{10,}\.[a-zA-Z0-9_\-]{10,}\.[a-zA-Z0-9_\-]+)|((?:ya29\.|ya28\.)[a-zA-Z0-9_\-]+)|((?:access_token|refresh_token|id_token)[""':\s=]+)(""?[a-zA-Z0-9_\-\.]{15,}""?)",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    static Logger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        LogDirPath = Path.Combine(appData, "AgyAccountSwarm");
        if (!Directory.Exists(LogDirPath))
        {
            Directory.CreateDirectory(LogDirPath);
        }
    }

    public static string LogDirectory => LogDirPath;

    public static string GetDailyLogFilePath(DateTime? date = null)
    {
        var targetDate = date ?? DateTime.Now;
        return Path.Combine(LogDirPath, $"agyswarm_{targetDate:yyyy-MM-dd}.log");
    }

    public static string LogPath => GetDailyLogFilePath();

    public static string RedactSensitive(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return TokenRegex.Replace(input, match =>
        {
            if (match.Groups[1].Success) return "[REDACTED_JWT_TOKEN]";
            if (match.Groups[2].Success) return "[REDACTED_OAUTH_TOKEN]";
            if (match.Groups[3].Success) return match.Groups[3].Value + "\"[REDACTED_TOKEN]\"";
            return "[REDACTED]";
        });
    }

    public static void Debug(string message)
    {
        Write("DEBUG", message);
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Success(string message)
    {
        Write("SUCCESS", message);
    }

    public static void Warn(string message)
    {
        Write("WARN", message);
    }

    public static void Warning(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        if (ex == null)
        {
            Write("ERROR", message);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append($"{message} | Exception: {ex.GetType().Name}: {ex.Message}");
        var inner = ex.InnerException;
        while (inner != null)
        {
            sb.Append($" --> Inner: {inner.GetType().Name}: {inner.Message}");
            inner = inner.InnerException;
        }
        if (!string.IsNullOrEmpty(ex.StackTrace))
        {
            sb.Append($"\n{ex.StackTrace}");
        }
        Write("ERROR", sb.ToString());
    }

    public static IReadOnlyList<string> GetRecentLogLines()
    {
        lock (LockObj)
        {
            if (RecentBuffer.Count > 0)
            {
                return RecentBuffer.ToList();
            }
        }

        try
        {
            var dailyPath = GetDailyLogFilePath();
            if (File.Exists(dailyPath))
            {
                return File.ReadAllLines(dailyPath);
            }
            var legacyPath = Path.Combine(LogDirPath, "app.log");
            if (File.Exists(legacyPath))
            {
                return File.ReadAllLines(legacyPath);
            }
        }
        catch
        {
            // Fallback
        }

        return Array.Empty<string>();
    }

    public static void Clear()
    {
        lock (LockObj)
        {
            RecentBuffer.Clear();
            try
            {
                var dailyPath = GetDailyLogFilePath();
                if (File.Exists(dailyPath))
                {
                    File.WriteAllText(dailyPath, string.Empty);
                }
            }
            catch
            {
                // Ignore file write errors during clear
            }
        }
    }

    public static int PruneOldLogFiles(int daysToKeep = 7)
    {
        lock (LockObj)
        {
            int deletedCount = 0;
            try
            {
                if (!Directory.Exists(LogDirPath)) return 0;
                var cutoff = DateTime.Now.Date.AddDays(-daysToKeep);
                var dirInfo = new DirectoryInfo(LogDirPath);
                var files = dirInfo.GetFiles("agyswarm_*.log")
                    .Concat(dirInfo.GetFiles("app*.log"));

                foreach (var file in files)
                {
                    bool shouldDelete = file.LastWriteTime < cutoff;
                    var match = System.Text.RegularExpressions.Regex.Match(file.Name, @"agyswarm_(\d{4}-\d{2}-\d{2})\.log");
                    if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var fileDate))
                    {
                        shouldDelete = fileDate < cutoff;
                    }

                    if (shouldDelete)
                    {
                        try
                        {
                            file.Delete();
                            deletedCount++;
                        }
                        catch
                        {
                            // File might be in use
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Warn($"[Logger] Error pruning old log files: {ex.Message}");
            }
            return deletedCount;
        }
    }

    private static void Write(string level, string message)
    {
        var safeMessage = RedactSensitive(message);
        var timestamp = DateTime.Now;
        var line = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {safeMessage}";
        lock (LockObj)
        {
            if (RecentBuffer.Count >= MaxBufferSize)
            {
                RecentBuffer.Dequeue();
            }
            RecentBuffer.Enqueue(line);

            try
            {
                var dailyPath = GetDailyLogFilePath(timestamp);
                File.AppendAllText(dailyPath, line + Environment.NewLine);
            }
            catch
            {
                // Do not crash if logging fails
            }
        }
    }
}
