using Microsoft.EntityFrameworkCore;
using RentTracker.Api.Data;
using RentTracker.Core.Rules;

namespace RentTracker.Api;

// /dashboard, /due-soon and /heatmap, ported from the Express API with the same JSON shapes.
public static class DashboardRoutes
{
    public static void MapDashboard(WebApplication app)
    {
        app.MapGet("/dashboard", Dashboard);
        app.MapGet("/due-soon", DueSoon);
        app.MapGet("/heatmap", Heatmap);
    }

    private static string StateLabel(decimal due, decimal paid) => PaymentStates.ToLabel(PaymentStates.For(due, paid));

    private static string Place(Tenancy t) => $"{t.Room.Property.Name} · {t.Room.Name}";

    private static int Percent(decimal part, decimal whole) =>
        whole == 0 ? 0 : (int)Math.Round(part / whole * 100, MidpointRounding.AwayFromZero);

    // Tenancies that overlap first..last, with renter, room and property loaded.
    private static Task<List<Tenancy>> TenanciesBetween(RentDb db, DateOnly first, DateOnly last) =>
        db.Tenancies
            .Include(t => t.Renter)
            .Include(t => t.Room).ThenInclude(r => r.Property)
            .Where(t => t.StartDate <= last && (t.EndDate == null || t.EndDate >= first))
            .OrderBy(t => t.StartDate)
            .ToListAsync();

    // Total paid per renter per month, keyed "renterId|month".
    private static async Task<Dictionary<string, decimal>> PaidByRenterMonth(RentDb db, IReadOnlyCollection<string> months)
    {
        var sums = await db.Payments
            .Where(p => months.Contains(p.Month))
            .GroupBy(p => new { p.RenterId, p.Month })
            .Select(g => new { g.Key.RenterId, g.Key.Month, Amount = g.Sum(p => p.Amount) })
            .ToListAsync();
        return sums.ToDictionary(s => $"{s.RenterId}|{s.Month}", s => s.Amount);
    }

    private static async Task<IResult> Dashboard(RentDb db, string? month)
    {
        if (!Months.IsMonth(month)) return ApiRoutes.Error(400, "month must be YYYY-MM.");

        var trendMonths = Months.Trailing(month!, 12);
        var rooms = await db.Rooms.Include(r => r.Property).ToListAsync();
        var tenancies = await TenanciesBetween(db, Months.FirstDay(trendMonths[0]), Months.LastDay(month!));
        var payments = await db.Payments
            .Where(p => trendMonths.Contains(p.Month))
            .Select(p => new { p.RenterId, p.Month, p.Amount })
            .ToListAsync();

        decimal DueIn(string m) => tenancies.Where(t => Tenancies.IsActiveIn(t.StartDate, t.EndDate, m)).Sum(t => t.MonthlyRent);
        decimal PaidIn(string m) => payments.Where(p => p.Month == m).Sum(p => p.Amount);

        var trend = trendMonths.Select(m =>
        {
            var due = Money.Round2(DueIn(m));
            var collected = Money.Round2(PaidIn(m));
            return new
            {
                Month = m,
                Due = due,
                Collected = collected,
                Outstanding = Money.Round2(Math.Max(0, due - collected)),
                Rate = due == 0 ? (int?)null : Percent(collected, due),
            };
        }).ToList();

        // The renters who live here this month, and what they have paid.
        var active = tenancies.Where(t => Tenancies.IsActiveIn(t.StartDate, t.EndDate, month!)).ToList();
        var paidBy = payments.Where(p => p.Month == month)
            .GroupBy(p => p.RenterId)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var today = ApiRoutes.Today();

        var rows = active.Select(t =>
        {
            var due = t.MonthlyRent;
            var paid = paidBy.GetValueOrDefault(t.RenterId);
            var dueDate = Months.DueDate(month!, t.DueDay);
            return new
            {
                Tenancy = t,
                t.RenterId,
                Name = t.Renter.Name,
                Place = Place(t),
                DueDate = dueDate,
                DaysLate = Math.Max(0, today.DayNumber - dueDate.DayNumber),
                Due = due,
                Paid = paid,
                Owed = Money.Round2(due - paid),
                State = StateLabel(due, paid),
            };
        }).ToList();

        var fullyPaid = rows.Count(r => r.State == "PAID");
        var overdue = rows
            .Where(r => r.Owed > 0 && r.DaysLate > 0)
            .OrderByDescending(r => r.DaysLate).ThenByDescending(r => r.Owed)
            .Select(r => new { r.RenterId, r.Name, r.Place, r.DueDate, r.DaysLate, r.Due, r.Paid, r.Owed, r.State })
            .ToList();

        // Rooms: occupied means a renter is living there this month.
        var occupiedIds = active.Select(t => t.RoomId).ToHashSet();
        var occupied = rooms.Count(r => occupiedIds.Contains(r.Id));
        var forRent = rooms.Where(r => !occupiedIds.Contains(r.Id) && r.Status == "AVAILABLE").ToList();

        var properties = rooms
            .GroupBy(r => new { r.PropertyId, r.Property.Name })
            .Select(g =>
            {
                var propertyRows = rows.Where(r => r.Tenancy.Room.PropertyId == g.Key.PropertyId).ToList();
                var count = g.Count();
                var occ = g.Count(r => occupiedIds.Contains(r.Id));
                return new
                {
                    Id = g.Key.PropertyId,
                    g.Key.Name,
                    Rooms = count,
                    Occupied = occ,
                    Occupancy = Percent(occ, count),
                    Due = Money.Round2(propertyRows.Sum(r => r.Due)),
                    Collected = Money.Round2(propertyRows.Sum(r => r.Paid)),
                };
            })
            .OrderBy(p => p.Name, StringComparer.CurrentCulture)
            .ToList();

        return Results.Ok(new
        {
            Month = month,
            Totals = new
            {
                Due = Money.Round2(rows.Sum(r => r.Due)),
                Collected = Money.Round2(PaidIn(month!)),
                Owed = Money.Round2(rows.Sum(r => Math.Max(0, r.Owed))),
                ActiveCount = rows.Count,
                FullyPaid = fullyPaid,
                PercentFullyPaid = Percent(fullyPaid, rows.Count),
                TotalRooms = rooms.Count,
                Occupied = occupied,
                Occupancy = Percent(occupied, rooms.Count),
                VacantRooms = forRent.Count,
                VacancyCost = Money.Round2(forRent.Sum(r => r.AskingRent ?? 0)),
                UnpricedVacant = forRent.Count(r => r.AskingRent == null),
            },
            Properties = properties,
            Overdue = overdue,
            Trend = trend,
        });
    }

    // Rent due in the next 7 days, from this month and next month.
    private static async Task<IResult> DueSoon(RentDb db)
    {
        var today = ApiRoutes.Today();
        var end = today.AddDays(7);
        var thisMonth = Months.MonthOf(today);
        string[] months = [thisMonth, Months.FromIndex(Months.Index(thisMonth) + 1)];

        var tenancies = await db.Tenancies
            .Include(t => t.Renter)
            .Include(t => t.Room).ThenInclude(r => r.Property)
            .Where(t => t.StartDate <= end && (t.EndDate == null || t.EndDate > today))
            .ToListAsync();
        var paidMap = await PaidByRenterMonth(db, months);

        var rows =
            from t in tenancies
            from m in months
            where Tenancies.IsActiveIn(t.StartDate, t.EndDate, m)
            let dueDate = Months.DueDate(m, t.DueDay)
            where dueDate >= today && dueDate <= end
            let paid = paidMap.GetValueOrDefault($"{t.RenterId}|{m}")
            orderby dueDate
            select new
            {
                t.RenterId,
                Name = t.Renter.Name,
                Place = Place(t),
                DueDate = dueDate,
                Due = t.MonthlyRent,
                Paid = paid,
                State = StateLabel(t.MonthlyRent, paid),
            };
        return Results.Ok(rows.ToList());
    }

    // Each tenant by month for the last 12 months up to the given month: paid, partly paid or unpaid.
    private static async Task<IResult> Heatmap(RentDb db, string? month)
    {
        var end = Months.IsMonth(month) ? month! : Months.MonthOf(ApiRoutes.Today());
        var months = Months.Trailing(end, 12);
        var tenancies = await TenanciesBetween(db, Months.FirstDay(months[0]), Months.LastDay(end));
        var paidMap = await PaidByRenterMonth(db, months);

        // One row per tenant per room. A tenant who left and came back to the same room
        // has several tenancies, so they are merged into one row.
        var rows = tenancies
            .GroupBy(t => (t.RenterId, t.RoomId))
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    TenancyId = first.Id,
                    first.RenterId,
                    Name = first.Renter.Name,
                    Place = Place(first),
                    Cells = months.Select(m =>
                    {
                        var activeTenancy = g.FirstOrDefault(t => Tenancies.IsActiveIn(t.StartDate, t.EndDate, m));
                        if (activeTenancy is null) return null;
                        var due = activeTenancy.MonthlyRent;
                        var paid = paidMap.GetValueOrDefault($"{g.Key.RenterId}|{m}");
                        return new { Month = m, Due = due, Paid = paid, State = StateLabel(due, paid) };
                    }).ToList(),
                };
            })
            .ToList();

        return Results.Ok(new { Months = months, Rows = rows });
    }
}
