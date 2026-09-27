using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AgyAccountSwarm.Services;

public static class Logger
{
    private static readonly object LockObj = new();
    private static readonly string LogFilePath;
    private static readonly List<string> RecentBuffer = new(1000);
    private const int MaxBufferSize = 1000;

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
        var full = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}" : message;
        Write("ERROR", full);
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
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        lock (LockObj)
        {
            if (RecentBuffer.Count >= MaxBufferSize)
            {
                RecentBuffer.RemoveAt(0);
            }
            RecentBuffer.Add(line);

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
