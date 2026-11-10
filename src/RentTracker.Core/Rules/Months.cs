using System.Globalization;
using System.Text.RegularExpressions;

namespace RentTracker.Core.Rules;

/// Month keys look like "2026-10". Dates look like "2026-10-26".
public static class Months
{
    private static readonly Regex MonthPattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    public static bool IsMonth(string? value) => value is not null && MonthPattern.IsMatch(value);

    public static bool IsDate(string? value) =>
        value is not null &&
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// "2026-10" becomes a number that counts months, so months can be compared and subtracted.
    public static int Index(string month)
    {
        var parts = month.Split('-');
        var year = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var monthNumber = int.Parse(parts[1], CultureInfo.InvariantCulture);
        return year * 12 + monthNumber - 1;
    }

    /// The reverse of Index.
    public static string FromIndex(int index) => $"{index / 12:0000}-{index % 12 + 1:00}";

    public static int IndexOf(DateOnly date) => date.Year * 12 + date.Month - 1;

    public static string MonthOf(DateOnly date) => $"{date.Year:0000}-{date.Month:00}";

    /// Every month from `from` to `to`, inclusive.
    public static IReadOnlyList<string> Range(string from, string to)
    {
        var months = new List<string>();
        for (var i = Index(from); i <= Index(to); i++)
        {
            months.Add(FromIndex(i));
        }
        return months;
    }

    /// The `count` months that end with `end`, oldest first.
    /// Trailing("2026-10", 3) gives 2026-08, 2026-09, 2026-10.
    public static IReadOnlyList<string> Trailing(string end, int count)
    {
        var months = new List<string>();
        for (var i = count - 1; i >= 0; i--)
        {
            months.Add(FromIndex(Index(end) - i));
        }
        return months;
    }

    public static DateOnly FirstDay(string month)
    {
        var index = Index(month);
        return new DateOnly(index / 12, index % 12 + 1, 1);
    }

    public static DateOnly LastDay(string month)
    {
        var index = Index(month);
        var year = index / 12;
        var monthNumber = index % 12 + 1;
        return new DateOnly(year, monthNumber, DateTime.DaysInMonth(year, monthNumber));
    }

    /// The date rent falls due in a month. A due day past the end of the month uses the last day.
    public static DateOnly DueDate(string month, int dueDay)
    {
        var last = LastDay(month);
        return new DateOnly(last.Year, last.Month, Math.Min(dueDay, last.Day));
    }
}
