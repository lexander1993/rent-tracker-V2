using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RentTracker.Api.Data;
using RentTracker.Core.Rules;

namespace RentTracker.Api;

// /renters/{id}/statement: a printable rent statement for one tenant, ported from the Express API.
public static class StatementRoutes
{
    public static void MapStatement(WebApplication app)
    {
        app.MapGet("/renters/{id:int}/statement", Statement);
    }

    private static bool TryDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static async Task<IResult> Statement(int id, RentDb db, string? from, string? to)
    {
        if (!TryDate(from, out var fromDate) || !TryDate(to, out var toDate))
            return ApiRoutes.Error(400, "Choose a valid from and to date.");
        if (toDate < fromDate) return ApiRoutes.Error(400, "The 'to' date can't be before the 'from' date.");

        var renter = await db.Renters.FindAsync(id);
        if (renter is null) return ApiRoutes.Error(404, "Tenant not found.");

        var months = Months.Range(Months.MonthOf(fromDate), Months.MonthOf(toDate));

        var tenancies = await db.Tenancies
            .Include(t => t.Room).ThenInclude(r => r.Property)
            .Where(t => t.RenterId == id && t.StartDate <= toDate && (t.EndDate == null || t.EndDate >= fromDate))
            .OrderBy(t => t.StartDate)
            .ToListAsync();

        var payments = await db.Payments
            .Where(p => p.RenterId == id && months.Contains(p.Month))
            .OrderBy(p => p.Month).ThenBy(p => p.Id)
            .ToListAsync();

        // The ledger starts in the month the tenant first moved in and stops at this month.
        var firstStart = await db.Tenancies
            .Where(t => t.RenterId == id)
            .OrderBy(t => t.StartDate)
            .Select(t => (DateOnly?)t.StartDate)
            .FirstOrDefaultAsync();
        var today = ApiRoutes.Today();
        var thisMonth = Months.Index(Months.MonthOf(today));
        var moveInMonth = firstStart is null ? (int?)null : Months.IndexOf(firstStart.Value);

        var ledger = months
            .Where(m => (moveInMonth is null || Months.Index(m) >= moveInMonth) && Months.Index(m) <= thisMonth)
            .Select(m =>
            {
                var due = Money.Round2(tenancies.Where(t => Tenancies.IsActiveIn(t.StartDate, t.EndDate, m)).Sum(t => t.MonthlyRent));
                var paid = Money.Round2(payments.Where(p => p.Month == m).Sum(p => p.Amount));
                return new { Month = m, Due = due, Paid = paid, Balance = Money.Round2(due - paid) };
            })
            .Where(row => row.Due != 0 || row.Paid != 0)
            .ToList();

        var totalDue = Money.Round2(ledger.Sum(r => r.Due));
        var totalPaid = Money.Round2(ledger.Sum(r => r.Paid));

        return Results.Ok(new
        {
            Renter = new { renter.Id, renter.Name, renter.Dob },
            From = from,
            To = to,
            GeneratedOn = today,
            MovedIn = firstStart,
            Tenancies = tenancies.Select(t => new
            {
                Place = $"{t.Room.Property.Name} · {t.Room.Name}",
                t.StartDate,
                EndDate = t.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                t.MonthlyRent,
                t.DueDay,
            }),
            Ledger = ledger,
            Payments = payments.Select(p => new { p.Month, p.Amount, p.PaidDate }),
            Totals = new { Due = totalDue, Paid = totalPaid, Balance = Money.Round2(totalDue - totalPaid) },
        });
    }
}
