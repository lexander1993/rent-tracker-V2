namespace RentTracker.Core.Rules;

public static class Tenancies
{
    /// A tenancy counts as active for a whole month if it covers any part of that month.
    public static bool IsActiveIn(DateOnly start, DateOnly? end, string month)
    {
        var index = Months.Index(month);
        return Months.IndexOf(start) <= index && (end is null || Months.IndexOf(end.Value) >= index);
    }

    /// Two tenancies clash when they share days.
    /// A tenancy that ends on the day another one starts does not clash with it.
    public static bool Overlaps(DateOnly existingStart, DateOnly? existingEnd, DateOnly newStart, DateOnly? newEnd)
    {
        var existingEndsAfterNewStarts = existingEnd is null || existingEnd.Value > newStart;
        var existingStartsBeforeNewEnds = newEnd is null || existingStart <= newEnd.Value;
        return existingEndsAfterNewStarts && existingStartsBeforeNewEnds;
    }

    /// Rent is due on the 1st to the 28th, so every month has that day.
    public static bool IsDueDayValid(int dueDay) => dueDay is >= 1 and <= 28;
}
