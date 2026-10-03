using System;

namespace AgyAccountSwarm.Models;

public class LogEntryItem
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string TimestampFormatted { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO"; // INFO, DEBUG, WARN, ERROR, SUCCESS
    public string Message { get; set; } = string.Empty;
    public string RawLine { get; set; } = string.Empty;

    public static LogEntryItem Parse(string rawLine)
    {
        var item = new LogEntryItem { RawLine = rawLine };
        if (string.IsNullOrWhiteSpace(rawLine)) return item;

        // format: [2026-10-04 00:11:33.123] [INFO] safeMessage
        int firstBracket = rawLine.IndexOf('[');
        int firstClose = rawLine.IndexOf(']');
        if (firstBracket >= 0 && firstClose > firstBracket)
        {
            var dateStr = rawLine.Substring(firstBracket + 1, firstClose - firstBracket - 1);
            if (DateTime.TryParse(dateStr, out var dt))
            {
                item.Timestamp = dt;
                item.TimestampFormatted = dt.ToString("HH:mm:ss.fff");
            }
            else
            {
                item.TimestampFormatted = dateStr;
            }

            int secondBracket = rawLine.IndexOf('[', firstClose + 1);
            if (secondBracket >= 0)
            {
                int secondClose = rawLine.IndexOf(']', secondBracket + 1);
                if (secondClose > secondBracket)
                {
                    item.Level = rawLine.Substring(secondBracket + 1, secondClose - secondBracket - 1).Trim().ToUpperInvariant();
                    item.Message = rawLine.Substring(secondClose + 1).TrimStart();
                    return item;
                }
            }
        }

        item.Message = rawLine;
        item.TimestampFormatted = DateTime.Now.ToString("HH:mm:ss");
        return item;
    }
}
