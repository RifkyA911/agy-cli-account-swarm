using System;
using System.IO;

namespace AgyAccountSwarm.Services;

public static class Logger
{
    private static readonly object LockObj = new();
    private static readonly string LogFilePath;

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

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warn(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        var full = ex != null ? $"{message}\nException: {ex}" : message;
        Write("ERROR", full);
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (LockObj)
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Do not crash if logging fails
        }
    }
}
