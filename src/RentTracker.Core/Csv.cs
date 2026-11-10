using System.Globalization;

namespace RentTracker.Core;

/// Writes CSV in the same way as the old API: every cell is quoted, and lines end with CRLF.
public static class Csv
{
    public static string Cell(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public static string Line(params object?[] cells) => string.Join(",", cells.Select(Cell));

    public static string Document(IEnumerable<string> lines) => string.Join("\r\n", lines);
}
