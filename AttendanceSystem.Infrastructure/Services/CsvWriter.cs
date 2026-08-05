using System.Globalization;
using System.Text;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Builds RFC-4180 CSV.
///
/// Two things here are not decoration:
///
/// A cell starting with = + - or @ is prefixed with an apostrophe. Excel and LibreOffice treat
/// such a cell as a formula, so an employee named "=cmd|..." becomes a command the moment an
/// administrator opens the export. The page scripts that already export CSV client-side do the
/// same thing; this is the server-side counterpart.
///
/// Output is UTF-8 *with* a BOM. Without it Excel reads the file as the local ANSI code page
/// and mangles every non-ASCII name — which, for a Sri Lankan staff list, is most of them.
/// </summary>
public static class CsvWriter
{
    public static string Cell(object? value)
    {
        var s = value switch
        {
            null => string.Empty,
            bool b => b ? "Yes" : "No",
            DateTime d => d.TimeOfDay == TimeSpan.Zero
                ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            TimeSpan t => t.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            double f => f.ToString(CultureInfo.InvariantCulture),
            byte[] _ => string.Empty,          // photos do not belong in a spreadsheet
            _ => value.ToString() ?? string.Empty
        };

        // Formula injection: neutralise before quoting, not after.
        if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '-' || s[0] == '@'))
            s = "'" + s;

        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    public static string Row(IEnumerable<object?> cells) =>
        string.Join(",", cells.Select(Cell));

    public static string Build(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(Row(header.Cast<object?>())).Append("\r\n");
        foreach (var r in rows) sb.Append(Row(r)).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>UTF-8 with BOM — see the note on this class.</summary>
    public static byte[] ToBytes(string csv) =>
        new UTF8Encoding(true).GetBytes(csv);

    public static byte[] Build(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows, bool asBytes) =>
        ToBytes(Build(header, rows));

    // ── Reading ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Splits one CSV line, honouring quotes and doubled quotes. Also strips the apostrophe
    /// that <see cref="Cell"/> adds, so a value survives an export/import round trip.
    /// </summary>
    public static List<string> SplitLine(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { cells.Add(Clean(sb.ToString())); sb.Clear(); }
            else sb.Append(c);
        }
        cells.Add(Clean(sb.ToString()));
        return cells;
    }

    private static string Clean(string s)
    {
        s = s.Trim();
        // Undo the formula guard added on the way out.
        if (s.Length > 1 && s[0] == '\'' && (s[1] == '=' || s[1] == '+' || s[1] == '-' || s[1] == '@'))
            s = s.Substring(1);
        return s;
    }

    /// <summary>Splits a whole file into lines, tolerating CRLF, LF and a leading BOM.</summary>
    public static List<List<string>> Parse(string content)
    {
        if (content.Length > 0 && content[0] == '﻿') content = content.Substring(1);

        return content
            .Replace("\r\n", "\n")
            .Split('\n')
            .Where(l => l.Trim().Length > 0)
            .Select(SplitLine)
            .ToList();
    }
}
