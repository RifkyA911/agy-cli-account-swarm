using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AgyAccountSwarm.Services;

public static class Logger
{
    private static readonly object LockObj = new();
    private static readonly string LogFilePath;
    private static readonly Queue<string> RecentBuffer = new(1000);
    private const int MaxBufferSize = 1000;

    private static readonly System.Text.RegularExpressions.Regex TokenRegex = new(
        @"(eyJ[a-zA-Z0-9_\-]{10,}\.[a-zA-Z0-9_\-]{10,}\.[a-zA-Z0-9_\-]+)|((?:ya29\.|ya28\.)[a-zA-Z0-9_\-]+)|((?:access_token|refresh_token|id_token)[""':\s=]+)(""?[a-zA-Z0-9_\-\.]{15,}""?)",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    static Logger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "AgyAccountSwarm");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        LogFilePath = Path.Combine(dir, "app.log");
    }

    public static string LogPath => LogFilePath;

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
            if (File.Exists(LogFilePath))
            {
                return File.ReadAllLines(LogFilePath);
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
                File.WriteAllText(LogFilePath, string.Empty);
            }
            catch
            {
                // Ignore file write errors during clear
            }
        }
    }

    private static void Write(string level, string message)
    {
        var safeMessage = RedactSensitive(message);
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {safeMessage}";
        lock (LockObj)
        {
            if (RecentBuffer.Count >= MaxBufferSize)
            {
                RecentBuffer.Dequeue();
            }
            RecentBuffer.Enqueue(line);

            try
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Do not crash if logging fails
            }
        }
    }
}
