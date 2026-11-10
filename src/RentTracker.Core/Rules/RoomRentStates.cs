namespace RentTracker.Core.Rules;

/// The colour of a room tile on the room map, from its current tenant.
public static class RoomRentStates
{
    public static string Compute(DateOnly start, decimal rent, decimal paid, int dueDay, DateOnly today, string thisMonth)
    {
        if (start > today)
        {
            return "UPCOMING";
        }
        if (paid >= rent)
        {
            return "PAID";
        }

        var daysUntilDue = Months.DueDate(thisMonth, dueDay).DayNumber - today.DayNumber;
        if (daysUntilDue < 0)
        {
            return "OVERDUE";
        }
        return daysUntilDue <= 7 ? "DUE_SOON" : "OPEN";
    }
}
