using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AgyAccountSwarm.Services;

public class LogExportItem
{
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO";
    public string Source { get; set; } = "App";
    public string Message { get; set; } = string.Empty;
}

public static class LogExcelExportService
{
    private static readonly Regex LogLineRegex = new(
        @"^\[(?<time>[^\]]+)\]\s+\[(?<level>[^\]]+)\]\s+(?:\[(?<source>[^\]]+)\]\s+)?(?<msg>.*)$",
        RegexOptions.Compiled);

    public static LogExportItem ParseLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new LogExportItem { Message = string.Empty };
        }

        var match = LogLineRegex.Match(line.Trim());
        if (match.Success)
        {
            return new LogExportItem
            {
                Timestamp = match.Groups["time"].Value.Trim(),
                Level = match.Groups["level"].Value.Trim().ToUpperInvariant(),
                Source = match.Groups["source"].Success ? match.Groups["source"].Value.Trim() : "App",
                Message = match.Groups["msg"].Value.Trim()
            };
        }

        return new LogExportItem
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Level = "INFO",
            Source = "App",
            Message = line.Trim()
        };
    }

    public static byte[] GenerateExcelWorkbook(IEnumerable<string> logLines)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            // 1. [Content_Types].xml
            WriteZipEntry(archive, "[Content_Types].xml", GetContentTypesXml());

            // 2. _rels/.rels
            WriteZipEntry(archive, "_rels/.rels", GetRootRelsXml());

            // 3. xl/_rels/workbook.xml.rels
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", GetWorkbookRelsXml());

            // 4. xl/workbook.xml
            WriteZipEntry(archive, "xl/workbook.xml", GetWorkbookXml());

            // 5. xl/styles.xml
            WriteZipEntry(archive, "xl/styles.xml", GetStylesXml());

            // 6. xl/worksheets/sheet1.xml
            WriteZipEntry(archive, "xl/worksheets/sheet1.xml", BuildSheetXml(logLines));
        }

        return memoryStream.ToArray();
    }

    public static async Task ExportToFileAsync(string filePath, IEnumerable<string> logLines)
    {
        var bytes = GenerateExcelWorkbook(logLines);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllBytesAsync(filePath, bytes);
        Logger.Info($"[LogExport] Successfully exported {bytes.Length} bytes to Excel workbook: '{filePath}'");
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string EscapeXml(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return SecurityElement.Escape(input) ?? string.Empty;
    }

    private static string GetContentTypesXml() =>
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
</Types>";

    private static string GetRootRelsXml() =>
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

    private static string GetWorkbookRelsXml() =>
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>";

    private static string GetWorkbookXml() =>
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets>
    <sheet name=""System Logs"" sheetId=""1"" r:id=""rId1""/>
  </sheets>
</workbook>";

    private static string GetStylesXml() =>
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""2"">
    <font>
      <sz val=""10""/>
      <color theme=""1""/>
      <name val=""Segoe UI""/>
      <family val=""2""/>
    </font>
    <font>
      <b/>
      <sz val=""10""/>
      <color rgb=""FFFFFFFF""/>
      <name val=""Segoe UI""/>
      <family val=""2""/>
    </font>
  </fonts>
  <fills count=""3"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill>
      <patternFill patternType=""solid"">
        <fgColor rgb=""FF1E293B""/>
        <bgColor indexed=""64""/>
      </patternFill>
    </fill>
  </fills>
  <borders count=""1"">
    <border>
      <left/><right/><top/><bottom/><diagonal/>
    </border>
  </borders>
  <cellStyleXfs count=""1"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/>
  </cellStyleXfs>
  <cellXfs count=""2"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""0"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyAlignment=""1"">
      <alignment horizontal=""center"" vertical=""center""/>
    </xf>
  </cellXfs>
</styleSheet>";

    private static string BuildSheetXml(IEnumerable<string> logLines)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">");

        // Column widths: Timestamp=24, Level=12, Source=20, Message=90
        sb.AppendLine(@"  <cols>");
        sb.AppendLine(@"    <col min=""1"" max=""1"" width=""24"" customWidth=""1""/>");
        sb.AppendLine(@"    <col min=""2"" max=""2"" width=""14"" customWidth=""1""/>");
        sb.AppendLine(@"    <col min=""3"" max=""3"" width=""22"" customWidth=""1""/>");
        sb.AppendLine(@"    <col min=""4"" max=""4"" width=""90"" customWidth=""1""/>");
        sb.AppendLine(@"  </cols>");

        sb.AppendLine(@"  <sheetData>");

        // Header Row (Row 1, s="1" for styled dark header)
        sb.AppendLine(@"    <row r=""1"">");
        sb.AppendLine(@"      <c r=""A1"" t=""inlineStr"" s=""1""><is><t>Timestamp (UTC/Local)</t></is></c>");
        sb.AppendLine(@"      <c r=""B1"" t=""inlineStr"" s=""1""><is><t>Log Level</t></is></c>");
        sb.AppendLine(@"      <c r=""C1"" t=""inlineStr"" s=""1""><is><t>Component / Source</t></is></c>");
        sb.AppendLine(@"      <c r=""D1"" t=""inlineStr"" s=""1""><is><t>Message Details</t></is></c>");
        sb.AppendLine(@"    </row>");

        int rowIndex = 2;
        foreach (var line in logLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var item = ParseLogLine(line);
            sb.AppendLine($@"    <row r=""{rowIndex}"">");
            sb.AppendLine($@"      <c r=""A{rowIndex}"" t=""inlineStr""><is><t>{EscapeXml(item.Timestamp)}</t></is></c>");
            sb.AppendLine($@"      <c r=""B{rowIndex}"" t=""inlineStr""><is><t>{EscapeXml(item.Level)}</t></is></c>");
            sb.AppendLine($@"      <c r=""C{rowIndex}"" t=""inlineStr""><is><t>{EscapeXml(item.Source)}</t></is></c>");
            sb.AppendLine($@"      <c r=""D{rowIndex}"" t=""inlineStr""><is><t>{EscapeXml(item.Message)}</t></is></c>");
            sb.AppendLine(@"    </row>");
            rowIndex++;
        }

        sb.AppendLine(@"  </sheetData>");
        sb.AppendLine(@"</worksheet>");

        return sb.ToString();
    }
}
