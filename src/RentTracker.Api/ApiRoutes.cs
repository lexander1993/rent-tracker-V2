using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RentTracker.Api.Data;
using RentTracker.Core;
using RentTracker.Core.Reports;
using RentTracker.Core.Rules;

namespace RentTracker.Api;

// Same routes and JSON shapes as the Express API in rent-tracker, so the React app works unchanged.
// /dashboard, /due-soon and /heatmap are in DashboardRoutes.cs; /renters/{id}/statement in StatementRoutes.cs.
// Not ported yet: the renter photo routes.
public static class ApiRoutes
{
    public static readonly string[] Categories =
    [
        "Repairs & maintenance", "Insurance", "Rates & land tax", "Agent / management fees",
        "Utilities", "Cleaning", "Advertising", "Legal & accounting", "Other",
    ];

    private static readonly string[] RoomStatuses = ["AVAILABLE", "REPAIR", "OFF_MARKET"];
    private static readonly string[] RenterStatuses = ["CURRENT", "MOVED_OUT"];

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static IResult Error(int status, string message) =>
        Results.Json(new { error = message }, statusCode: status);

    public static void MapRentTrackerApi(this WebApplication app)
    {
        app.MapGet("/categories", () => Results.Ok(Categories));

        MapRenters(app);
        MapProperties(app);
        MapRooms(app);
        MapTenancies(app);
        MapPayments(app);
        MapStatus(app);
        MapExpenses(app);
        MapReports(app);
        DashboardRoutes.MapDashboard(app);
        StatementRoutes.MapStatement(app);
    }

    // ----- Renters -----

    private static void MapRenters(WebApplication app)
    {
        // room is where the renter lives now (latest tenancy that has not ended), or null.
        // hasPhoto is always false until the photo routes are ported.
        app.MapGet("/renters", async Task<IResult> (RentDb db) =>
        {
            var today = Today();
            return Results.Ok(await db.Renters.OrderBy(r => r.Name)
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Dob,
                    r.Status,
                    r.CreatedAt,
                    HasPhoto = false,
                    Room = db.Tenancies
                        .Where(t => t.RenterId == r.Id && (t.EndDate == null || t.EndDate > today))
                        .OrderByDescending(t => t.StartDate)
                        .Select(t => t.Room.Property.Name + " · " + t.Room.Name)
                        .FirstOrDefault(),
                })
                .ToListAsync());
        });

        app.MapPost("/renters", async Task<IResult> (RentDb db, RenterInput input) =>
        {
            var name = input.Name?.Trim() ?? "";
            if (name.Length == 0) return Error(400, "Name is required.");

            DateOnly? dob = null;
            if (input.Dob is not null)
            {
                if (!TryDate(input.Dob, out var parsed)) return Error(400, "Date of birth must be YYYY-MM-DD.");
                dob = parsed;
            }

            var renter = new Renter { Name = name, Dob = dob };
            db.Renters.Add(renter);
            await db.SaveChangesAsync();
            return Results.Created($"/renters/{renter.Id}", RenterDto(renter));
        });

        app.MapPatch("/renters/{id:int}", async Task<IResult> (int id, RentDb db, RenterPatch body) =>
        {
            var renter = await db.Renters.FindAsync(id);
            if (renter is null) return Error(404, "Renter not found.");

            if (body.Name is not null)
            {
                var name = body.Name.Trim();
                if (name.Length == 0) return Error(400, "Name is required.");
                renter.Name = name;
            }

            if (body.Dob is not null)
            {
                if (!TryDate(body.Dob, out var dob)) return Error(400, "Date of birth must be YYYY-MM-DD.");
                renter.Dob = dob;
            }

            if (body.Status is not null)
            {
                if (!RenterStatuses.Contains(body.Status)) return Error(400, "Unknown renter status.");
                renter.Status = body.Status;

                if (body.Status == "MOVED_OUT")
                {
                    // Moving out ends every tenancy that has started, from today.
                    var today = Today();
                    var open = await db.Tenancies
                        .Where(t => t.RenterId == id && t.StartDate <= today && (t.EndDate == null || t.EndDate > today))
                        .ToListAsync();
                    foreach (var tenancy in open) tenancy.EndDate = today;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(RenterDto(renter));
        });

        app.MapDelete("/renters/{id:int}", async Task<IResult> (int id, RentDb db) =>
        {
            var renter = await db.Renters.FindAsync(id);
            if (renter is null) return Error(404, "Renter not found.");

            if (await db.Tenancies.AnyAsync(t => t.RenterId == id) || await db.Payments.AnyAsync(p => p.RenterId == id))
            {
                return Error(409, "This renter has tenancies or payments. Remove those first.");
            }

            db.Renters.Remove(renter);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ----- Properties -----

    private static void MapProperties(WebApplication app)
    {
        app.MapGet("/properties", async Task<IResult> (RentDb db) =>
            Results.Ok(await db.Properties.OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    Address = p.Address ?? "",
                    RoomCount = db.Rooms.Count(r => r.PropertyId == p.Id),
                })
                .ToListAsync()));

        app.MapPost("/properties", async Task<IResult> (RentDb db, PropertyInput input) =>
        {
            var name = input.Name?.Trim() ?? "";
            if (name.Length == 0) return Error(400, "Property name is required.");

            var property = new Property { Name = name, Address = string.IsNullOrWhiteSpace(input.Address) ? null : input.Address.Trim() };
            db.Properties.Add(property);
            await db.SaveChangesAsync();
            return Results.Created($"/properties/{property.Id}", new { property.Id, property.Name, property.Address });
        });

        app.MapDelete("/properties/{id:int}", async Task<IResult> (int id, RentDb db) =>
        {
            var property = await db.Properties.FindAsync(id);
            if (property is null) return Error(404, "Property not found.");

            if (await db.Rooms.AnyAsync(r => r.PropertyId == id) || await db.Expenses.AnyAsync(e => e.PropertyId == id))
            {
                return Error(409, "This property still has rooms or expenses. Remove those first.");
            }

            db.Properties.Remove(property);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ----- Rooms -----

    private static void MapRooms(WebApplication app)
    {
        app.MapGet("/rooms", async Task<IResult> (RentDb db, int? propertyId) =>
            Results.Ok(await LoadRooms(db, propertyId)));

        app.MapPost("/rooms", async Task<IResult> (RentDb db, RoomInput input) =>
        {
            var name = input.Name?.Trim() ?? "";
            if (name.Length == 0) return Error(400, "Room name is required.");
            if (!await db.Properties.AnyAsync(p => p.Id == input.PropertyId)) return Error(404, "Property not found.");
            if (input.AskingRent is not null && input.AskingRent <= 0) return Error(400, "Asking rent must be more than 0.");

            var status = input.Status ?? "AVAILABLE";
            if (!RoomStatuses.Contains(status)) return Error(400, "Unknown room status.");

            if (await db.Rooms.AnyAsync(r => r.PropertyId == input.PropertyId && r.Name == name))
            {
                return Error(409, "A room with that name already exists in this property.");
            }

            var room = new Room
            {
                PropertyId = input.PropertyId,
                Name = name,
                Status = status,
                AskingRent = input.AskingRent is null ? null : Money.Round2(input.AskingRent.Value),
            };
            db.Rooms.Add(room);
            await db.SaveChangesAsync();
            return Results.Created($"/rooms/{room.Id}", RoomDto(room));
        });

        app.MapPatch("/rooms/{id:int}", async Task<IResult> (int id, RentDb db, RoomPatch body) =>
        {
            var room = await db.Rooms.FindAsync(id);
            if (room is null) return Error(404, "Room not found.");

            if (body.Status is not null)
            {
                if (!RoomStatuses.Contains(body.Status)) return Error(400, "Unknown room status.");
                room.Status = body.Status;
            }

            // A asking rent of 0 or less clears it.
            if (body.AskingRent is not null)
            {
                room.AskingRent = body.AskingRent <= 0 ? null : Money.Round2(body.AskingRent.Value);
            }

            await db.SaveChangesAsync();
            return Results.Ok(RoomDto(room));
        });

        app.MapDelete("/rooms/{id:int}", async Task<IResult> (int id, RentDb db) =>
        {
            var room = await db.Rooms.FindAsync(id);
            if (room is null) return Error(404, "Room not found.");

            var today = Today();
            if (await db.Tenancies.AnyAsync(t => t.RoomId == id && (t.EndDate == null || t.EndDate > today)))
            {
                return Error(409, "This room has a current tenant. End the tenancy first.");
            }

            // Past tenancies go with the room. Payments belong to the renter, so they stay.
            db.Tenancies.RemoveRange(db.Tenancies.Where(t => t.RoomId == id));
            db.Rooms.Remove(room);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ----- Tenancies -----

    private static void MapTenancies(WebApplication app)
    {
        app.MapGet("/tenancies", async Task<IResult> (RentDb db, int? roomId) =>
        {
            var query = db.Tenancies.AsQueryable();
            if (roomId is not null) query = query.Where(t => t.RoomId == roomId);

            return Results.Ok(await query.OrderByDescending(t => t.StartDate)
                .Select(t => new
                {
                    t.Id, t.RoomId, t.RenterId, RenterName = t.Renter.Name,
                    Place = t.Room.Property.Name + " · " + t.Room.Name,
                    t.StartDate, t.EndDate, t.MonthlyRent, t.DueDay,
                })
                .ToListAsync());
        });

        app.MapPost("/tenancies", async Task<IResult> (RentDb db, TenancyInput input) =>
        {
            if (!TryDate(input.StartDate, out var start)) return Error(400, "Start date must be YYYY-MM-DD.");

            DateOnly? end = null;
            if (input.EndDate is not null)
            {
                if (!TryDate(input.EndDate, out var parsedEnd)) return Error(400, "End date must be YYYY-MM-DD.");
                if (parsedEnd < start) return Error(400, "End date cannot be before the start date.");
                end = parsedEnd;
            }

            var rent = input.MonthlyRent ?? 0;
            if (rent <= 0) return Error(400, "Monthly rent must be more than 0.");

            var dueDay = input.DueDay ?? 1;
            if (!Tenancies.IsDueDayValid(dueDay)) return Error(400, "Due day must be between 1 and 28.");

            if (!await db.Rooms.AnyAsync(r => r.Id == input.RoomId)) return Error(404, "Room not found.");
            var renter = await db.Renters.FindAsync(input.RenterId);
            if (renter is null) return Error(404, "Renter not found.");

            if (await HasClash(db, input.RoomId, start, end, null))
            {
                return Error(409, "This room already has a tenancy in those dates.");
            }

            var tenancy = new Tenancy
            {
                RoomId = input.RoomId,
                RenterId = input.RenterId,
                StartDate = start,
                EndDate = end,
                MonthlyRent = Money.Round2(rent),
                DueDay = dueDay,
            };
            db.Tenancies.Add(tenancy);
            await db.SaveChangesAsync();
            await SyncRenterStatus(db, renter.Id);
            return Results.Created($"/tenancies/{tenancy.Id}", TenancyDto(tenancy));
        });

        app.MapPatch("/tenancies/{id:int}/end", async Task<IResult> (int id, RentDb db, EndTenancyInput body) =>
        {
            var tenancy = await db.Tenancies.FindAsync(id);
            if (tenancy is null) return Error(404, "Tenancy not found.");
            if (!TryDate(body.EndDate, out var end)) return Error(400, "End date must be YYYY-MM-DD.");
            if (end < tenancy.StartDate) return Error(400, "End date cannot be before the start date.");

            tenancy.EndDate = end;
            await db.SaveChangesAsync();
            await SyncRenterStatus(db, tenancy.RenterId);
            return Results.Ok(TenancyDto(tenancy));
        });

        app.MapPatch("/tenancies/{id:int}", async Task<IResult> (int id, RentDb db, TenancyPatch body) =>
        {
            var tenancy = await db.Tenancies.FindAsync(id);
            if (tenancy is null) return Error(404, "Tenancy not found.");
            if (body.StartDate is null && body.MonthlyRent is null && body.DueDay is null) return Error(400, "Nothing to change.");

            var start = tenancy.StartDate;
            if (body.StartDate is not null)
            {
                if (!TryDate(body.StartDate, out start)) return Error(400, "Start date must be YYYY-MM-DD.");
                if (tenancy.EndDate is not null && tenancy.EndDate < start) return Error(400, "Start date cannot be after the end date.");
                if (await HasClash(db, tenancy.RoomId, start, tenancy.EndDate, tenancy.Id))
                {
                    return Error(409, "This room already has a tenancy in those dates.");
                }
            }

            if (body.MonthlyRent is not null)
            {
                if (body.MonthlyRent <= 0) return Error(400, "Monthly rent must be more than 0.");
                tenancy.MonthlyRent = Money.Round2(body.MonthlyRent.Value);
            }

            if (body.DueDay is not null)
            {
                if (!Tenancies.IsDueDayValid(body.DueDay.Value)) return Error(400, "Due day must be between 1 and 28.");
                tenancy.DueDay = body.DueDay.Value;
            }

            tenancy.StartDate = start;
            await db.SaveChangesAsync();
            return Results.Ok(TenancyDto(tenancy));
        });

        // Ends the current tenancy on the start date and starts a new one for another renter, in one transaction.
        app.MapPost("/tenancies/{id:int}/change-tenant", async Task<IResult> (int id, RentDb db, ChangeTenantInput body) =>
        {
            if (!TryDate(body.StartDate, out var start)) return Error(400, "Start date must be YYYY-MM-DD.");

            var rent = body.MonthlyRent ?? 0;
            if (rent <= 0) return Error(400, "Monthly rent must be more than 0.");

            var dueDay = body.DueDay ?? 1;
            if (!Tenancies.IsDueDayValid(dueDay)) return Error(400, "Due day must be between 1 and 28.");

            var old = await db.Tenancies.FindAsync(id);
            if (old is null) return Error(404, "Tenancy not found.");
            if (start <= old.StartDate) return Error(400, "The new tenant must move in after the current tenant started.");
            if (old.EndDate is not null && old.EndDate <= start) return Error(400, "This tenancy has already ended.");

            var renter = await db.Renters.FindAsync(body.RenterId);
            if (renter is null) return Error(404, "Renter not found.");

            await using var transaction = await db.Database.BeginTransactionAsync();

            var previousRenterId = old.RenterId;
            old.EndDate = start;

            if (await HasClash(db, old.RoomId, start, null, old.Id))
            {
                return Error(409, "This room already has another tenancy after that date.");
            }

            var replacement = new Tenancy
            {
                RoomId = old.RoomId,
                RenterId = body.RenterId,
                StartDate = start,
                EndDate = null,
                MonthlyRent = Money.Round2(rent),
                DueDay = dueDay,
            };
            db.Tenancies.Add(replacement);
            await db.SaveChangesAsync();
            await SyncRenterStatus(db, previousRenterId);
            await SyncRenterStatus(db, replacement.RenterId);
            await transaction.CommitAsync();

            return Results.Ok(TenancyDto(replacement));
        });

        // Only a duplicate tenancy (same renter, same room) can be deleted. Otherwise end it instead.
        app.MapDelete("/tenancies/{id:int}", async Task<IResult> (int id, RentDb db) =>
        {
            var tenancy = await db.Tenancies.FindAsync(id);
            if (tenancy is null) return Error(404, "Tenancy not found.");

            var hasDuplicate = await db.Tenancies.AnyAsync(t =>
                t.Id != id && t.RoomId == tenancy.RoomId && t.RenterId == tenancy.RenterId);
            if (!hasDuplicate) return Error(409, "Only a duplicate tenancy can be deleted. End the tenancy instead.");

            var renterId = tenancy.RenterId;
            db.Tenancies.Remove(tenancy);
            await db.SaveChangesAsync();
            await SyncRenterStatus(db, renterId);
            return Results.NoContent();
        });
    }

    // ----- Payments -----

    private static void MapPayments(WebApplication app)
    {
        app.MapGet("/payments", async Task<IResult> (RentDb db, int? renterId) =>
        {
            var query = db.Payments.AsQueryable();
            if (renterId is not null) query = query.Where(p => p.RenterId == renterId);

            return Results.Ok(await query.OrderByDescending(p => p.Month).ThenByDescending(p => p.Id)
                .Select(p => new { p.Id, p.RenterId, RenterName = p.Renter.Name, p.Month, p.Amount, p.PaidDate })
                .ToListAsync());
        });

        app.MapPost("/payments", async Task<IResult> (RentDb db, PaymentInput input) =>
        {
            if (!Months.IsMonth(input.Month)) return Error(400, "Month must be YYYY-MM.");
            if (input.Amount is null || input.Amount <= 0) return Error(400, "Amount must be more than 0.");

            DateOnly paidDate = Today();
            if (input.PaidDate is not null)
            {
                if (!TryDate(input.PaidDate, out paidDate)) return Error(400, "Paid date must be YYYY-MM-DD.");
            }

            if (!await db.Renters.AnyAsync(r => r.Id == input.RenterId)) return Error(404, "Renter not found.");

            var payment = new Payment
            {
                RenterId = input.RenterId,
                Month = input.Month!,
                Amount = Money.Round2(input.Amount.Value),
                PaidDate = paidDate,
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            return Results.Created($"/payments/{payment.Id}", PaymentDto(payment));
        });

        app.MapPatch("/payments/{id:int}", async Task<IResult> (int id, RentDb db, PaymentPatch body) =>
        {
            var payment = await db.Payments.FindAsync(id);
            if (payment is null) return Error(404, "Payment not found.");

            if (body.Month is not null)
            {
                if (!Months.IsMonth(body.Month)) return Error(400, "Month must be YYYY-MM.");
                payment.Month = body.Month;
            }

            if (body.Amount is not null)
            {
                if (body.Amount <= 0) return Error(400, "Amount must be more than 0.");
                payment.Amount = Money.Round2(body.Amount.Value);
            }

            if (body.PaidDate is not null)
            {
                if (!TryDate(body.PaidDate, out var paidDate)) return Error(400, "Paid date must be YYYY-MM-DD.");
                payment.PaidDate = paidDate;
            }

            await db.SaveChangesAsync();
            return Results.Ok(PaymentDto(payment));
        });

        app.MapDelete("/payments/{id:int}", async Task<IResult> (int id, RentDb db) =>
        {
            var payment = await db.Payments.FindAsync(id);
            if (payment is null) return Error(404, "Payment not found.");

            db.Payments.Remove(payment);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ----- Status (who has paid this month) -----

    private static void MapStatus(WebApplication app)
    {
        app.MapGet("/status", async Task<IResult> (RentDb db, string? month) =>
        {
            var target = month ?? Months.MonthOf(Today());
            if (!Months.IsMonth(target)) return Error(400, "Month must be YYYY-MM.");

            var tenancies = await db.Tenancies
                .Include(t => t.Room).ThenInclude(r => r.Property)
                .Include(t => t.Renter)
                .ToListAsync();

            var active = tenancies.Where(t => Tenancies.IsActiveIn(t.StartDate, t.EndDate, target)).ToList();
            var renterIds = active.Select(t => t.RenterId).Distinct().ToList();

            var paidByRenter = await db.Payments
                .Where(p => p.Month == target && renterIds.Contains(p.RenterId))
                .GroupBy(p => p.RenterId)
                .Select(g => new { RenterId = g.Key, Amount = g.Sum(p => p.Amount) })
                .ToDictionaryAsync(x => x.RenterId, x => x.Amount);

            var rows = active.Select(t =>
            {
                var paid = paidByRenter.GetValueOrDefault(t.RenterId);
                return new
                {
                    tenancyId = t.Id,
                    renterId = t.RenterId,
                    propertyId = t.Room.PropertyId,
                    name = t.Renter.Name,
                    place = $"{t.Room.Property.Name} · {t.Room.Name}",
                    monthlyRent = t.MonthlyRent,
                    paid,
                    state = PaymentStates.ToLabel(PaymentStates.For(t.MonthlyRent, paid)),
                };
            })
            .OrderBy(r => r.place, StringComparer.CurrentCulture)
            .ToList();

            return Results.Ok(rows);
        });
    }

    // ----- Expenses -----

    private static void MapExpenses(WebApplication app)
    {
        app.MapGet("/expenses", async Task<IResult> (RentDb db, int? propertyId, string? from, string? to) =>
        {
            var query = db.Expenses.AsQueryable();
            if (propertyId is not null) query = query.Where(e => e.PropertyId == propertyId);
            if (from is not null)
            {
                if (!TryDate(from, out var f)) return Error(400, "from must be YYYY-MM-DD.");
                query = query.Where(e => e.Date >= f);
            }
            if (to is not null)
            {
                if (!TryDate(to, out var t)) return Error(400, "to must be YYYY-MM-DD.");
                query = query.Where(e => e.Date <= t);
            }

            return Results.Ok(await query.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id)
                .Select(e => new { e.Id, e.PropertyId, PropertyName = e.Property.Name, e.Date, e.Category, e.Amount, e.Note })
                .ToListAsync());
        });

        app.MapPost("/expenses", async Task<IResult> (RentDb db, ExpenseInput input) =>
        {
            if (!await db.Properties.AnyAsync(p => p.Id == input.PropertyId)) return Error(404, "Property not found.");
            if (!TryDate(input.Date, out var date)) return Error(400, "Date must be YYYY-MM-DD.");
            if (input.Category is null || !Categories.Contains(input.Category)) return Error(400, "Unknown expense category.");
            if (input.Amount is null || input.Amount <= 0) return Error(400, "Amount must be more than 0.");

            var expense = new Expense
            {
                PropertyId = input.PropertyId,
                Date = date,
                Category = input.Category,
                Amount = Money.Round2(input.Amount.Value),
                Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            };
            db.Expenses.Add(expense);
            await db.SaveChangesAsync();
            return Results.Created($"/expenses/{expense.Id}", ExpenseDto(expense));
        });

        app.MapDelete("/expenses/{id:int}", async Task<IResult> (int id, RentDb db) =>
        {
            var expense = await db.Expenses.FindAsync(id);
            if (expense is null) return Error(404, "Expense not found.");

            db.Expenses.Remove(expense);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // ----- Reports -----

    private static void MapReports(WebApplication app)
    {
        app.MapGet("/reports/monthly", async Task<IResult> (RentDb db, string? from, string? to) =>
        {
            if (!ValidMonthRange(from, to, out var error)) return Error(400, error);
            var facts = await LoadMonthFacts(db, from!, to!);
            var report = MonthlyReport.Build(from!, to!, facts.Tenancies, facts.Payments, facts.Expenses);
            return Results.Ok(new { from, to, report.Months, report.Totals });
        });

        app.MapGet("/reports/profit", async Task<IResult> (RentDb db, string? from, string? to) =>
        {
            if (!ValidMonthRange(from, to, out var error)) return Error(400, error);
            var facts = await LoadMonthFacts(db, from!, to!);
            var report = ProfitReport.Build(from!, to!, facts.Properties, facts.Tenancies, facts.Payments, facts.Expenses);
            return Results.Ok(new { from, to, report.Properties, report.Totals });
        });

        app.MapGet("/reports/expenses", async Task<IResult> (RentDb db, string? from, string? to) =>
        {
            if (!TryDate(from, out var f) || !TryDate(to, out var t)) return Error(400, "from and to must be YYYY-MM-DD dates.");
            if (t < f) return Error(400, "to must be on or after from.");
            var report = ExpenseReport.Build(await LoadExpenses(db, f, t));
            return Results.Ok(new { from, to, report.Total, report.ByCategory, report.ByProperty, report.Items });
        });

        app.MapGet("/reports/monthly.csv", async Task<IResult> (RentDb db, string? from, string? to) =>
        {
            if (!ValidMonthRange(from, to, out var error)) return Error(400, error);
            var facts = await LoadMonthFacts(db, from!, to!);
            var report = MonthlyReport.Build(from!, to!, facts.Tenancies, facts.Payments, facts.Expenses);

            var lines = new List<string> { Csv.Line("Month", "Charged", "Collected", "Arrears", "Expenses", "Profit") };
            lines.AddRange(report.Months.Select(r => Csv.Line(r.Month, r.Charged, r.Collected, r.Arrears, r.Expenses, r.Profit)));
            lines.Add(Csv.Line("Total", report.Totals.Charged, report.Totals.Collected, report.Totals.Arrears, report.Totals.Expenses, report.Totals.Profit));
            return CsvFile(lines, $"monthly-{from}-to-{to}.csv");
        });

        app.MapGet("/reports/profit.csv", async Task<IResult> (RentDb db, string? from, string? to) =>
        {
            if (!ValidMonthRange(from, to, out var error)) return Error(400, error);
            var facts = await LoadMonthFacts(db, from!, to!);
            var report = ProfitReport.Build(from!, to!, facts.Properties, facts.Tenancies, facts.Payments, facts.Expenses);

            var lines = new List<string> { Csv.Line("Property", "Charged", "Collected", "Arrears", "Expenses", "Profit") };
            lines.AddRange(report.Properties.Select(p => Csv.Line(p.Name, p.Charged, p.Collected, p.Arrears, p.Expenses, p.Profit)));
            lines.Add(Csv.Line("Total", report.Totals.Charged, report.Totals.Collected, report.Totals.Arrears, report.Totals.Expenses, report.Totals.Profit));
            return CsvFile(lines, $"profit-{from}-to-{to}.csv");
        });

        app.MapGet("/reports/expenses.csv", async Task<IResult> (RentDb db, string? from, string? to) =>
        {
            if (!TryDate(from, out var f) || !TryDate(to, out var t)) return Error(400, "from and to must be YYYY-MM-DD dates.");
            if (t < f) return Error(400, "to must be on or after from.");
            var report = ExpenseReport.Build(await LoadExpenses(db, f, t));

            var lines = new List<string> { Csv.Line("Date", "Property", "Category", "Note", "Amount") };
            lines.AddRange(report.Items.Select(e => Csv.Line(
                e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.PropertyName, e.Category, e.Note, e.Amount)));
            lines.Add(Csv.Line("", "", "Total", "", report.Total));
            return CsvFile(lines, $"expenses-{from}-to-{to}.csv");
        });
    }

    // ----- Shared helpers -----

    private static async Task<List<RoomView>> LoadRooms(RentDb db, int? propertyId)
    {
        var today = Today();
        var thisMonth = Months.MonthOf(today);

        var roomQuery = db.Rooms.Include(r => r.Property).AsQueryable();
        if (propertyId is not null) roomQuery = roomQuery.Where(r => r.PropertyId == propertyId);
        var rooms = await roomQuery.OrderBy(r => r.Property.Name).ThenBy(r => r.Name).ToListAsync();

        var roomIds = rooms.Select(r => r.Id).ToList();
        var current = await db.Tenancies
            .Include(t => t.Renter)
            .Where(t => roomIds.Contains(t.RoomId) && (t.EndDate == null || t.EndDate > today))
            .ToListAsync();

        var renterIds = current.Select(t => t.RenterId).Distinct().ToList();
        var paidByRenter = await db.Payments
            .Where(p => p.Month == thisMonth && renterIds.Contains(p.RenterId))
            .GroupBy(p => p.RenterId)
            .Select(g => new { RenterId = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.RenterId, x => x.Amount);

        return rooms.Select(room =>
        {
            var tenancy = current.FirstOrDefault(t => t.RoomId == room.Id);
            if (tenancy is null)
            {
                return new RoomView(room.Id, room.PropertyId, room.Property.Name, room.Name, room.Status, room.AskingRent,
                    null, null, null, null, null, null, null, null);
            }

            var paid = paidByRenter.GetValueOrDefault(tenancy.RenterId);
            var state = RoomRentStates.Compute(tenancy.StartDate, tenancy.MonthlyRent, paid, tenancy.DueDay, today, thisMonth);
            return new RoomView(room.Id, room.PropertyId, room.Property.Name, room.Name, room.Status, room.AskingRent,
                tenancy.Id, tenancy.RenterId, tenancy.Renter.Name, tenancy.StartDate, tenancy.EndDate,
                tenancy.MonthlyRent, tenancy.DueDay, state);
        }).ToList();
    }

    private static async Task<bool> HasClash(RentDb db, int roomId, DateOnly start, DateOnly? end, int? excludeId)
    {
        var others = await db.Tenancies
            .Where(t => t.RoomId == roomId && (excludeId == null || t.Id != excludeId))
            .ToListAsync();
        return others.Any(t => Tenancies.Overlaps(t.StartDate, t.EndDate, start, end));
    }

    // Renter is CURRENT while any tenancy is open or still running; otherwise MOVED_OUT.
    // Call after SaveChanges, because it reads the database.
    private static async Task SyncRenterStatus(RentDb db, int renterId)
    {
        var today = Today();
        var stillRenting = await db.Tenancies.AnyAsync(t =>
            t.RenterId == renterId && (t.EndDate == null || t.EndDate > today));

        var renter = await db.Renters.FindAsync(renterId);
        if (renter is null) return;

        renter.Status = stillRenting ? "CURRENT" : "MOVED_OUT";
        await db.SaveChangesAsync();
    }

    // from/to are YYYY-MM or YYYY-MM-DD. Rent counts by whole month; expenses use the exact dates.
    private static async Task<ReportFacts> LoadMonthFacts(RentDb db, string from, string to)
    {
        var months = Months.Range(from[..7], to[..7]);
        var monthStart = Months.FirstDay(from[..7]);
        var monthEnd = Months.LastDay(to[..7]);
        var firstDay = TryDate(from, out var f) ? f : monthStart;
        var lastDay = TryDate(to, out var t) ? t : monthEnd;

        var tenancies = await db.Tenancies
            .Where(t => t.StartDate <= monthEnd && (t.EndDate == null || t.EndDate >= monthStart))
            .Select(t => new TenancyFact(t.Room.PropertyId, t.RenterId, t.StartDate, t.EndDate, t.MonthlyRent))
            .ToListAsync();

        var payments = await db.Payments
            .Where(p => months.Contains(p.Month))
            .Select(p => new PaymentFact(p.RenterId, p.Month, p.Amount))
            .ToListAsync();

        var expenses = await LoadExpenses(db, firstDay, lastDay);

        var properties = await db.Properties
            .Select(p => new PropertyName(p.Id, p.Name))
            .ToListAsync();

        return new ReportFacts(tenancies, payments, expenses, properties);
    }

    private static Task<List<ExpenseFact>> LoadExpenses(RentDb db, DateOnly from, DateOnly to) =>
        db.Expenses
            .Where(e => e.Date >= from && e.Date <= to)
            .Select(e => new ExpenseFact(e.Id, e.PropertyId, e.Property.Name, e.Date, e.Category, e.Note ?? "", e.Amount))
            .ToListAsync();

    // Accepts YYYY-MM months or YYYY-MM-DD dates (the React report page sends dates).
    private static bool ValidMonthRange(string? from, string? to, out string error)
    {
        static bool Valid(string? v) => Months.IsMonth(v) || TryDate(v, out _);
        if (!Valid(from) || !Valid(to))
        {
            error = "Choose a valid from and to date.";
            return false;
        }
        var toBeforeFrom = TryDate(from, out var f) && TryDate(to, out var t)
            ? t < f
            : Months.Index(to![..7]) < Months.Index(from![..7]);
        if (toBeforeFrom)
        {
            error = "The 'to' date can't be before the 'from' date.";
            return false;
        }
        error = "";
        return true;
    }

    private static bool TryDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static IResult CsvFile(List<string> lines, string fileName) =>
        Results.File(Encoding.UTF8.GetBytes(Csv.Document(lines)), "text/csv; charset=utf-8", fileName);

    private static object RenterDto(Renter r) => new { r.Id, r.Name, r.Dob, r.Status, r.CreatedAt };
    private static object RoomDto(Room r) => new { r.Id, r.PropertyId, r.Name, r.Status, r.AskingRent };
    private static object TenancyDto(Tenancy t) => new { t.Id, t.RoomId, t.RenterId, t.StartDate, t.EndDate, t.MonthlyRent, t.DueDay };
    private static object PaymentDto(Payment p) => new { p.Id, p.RenterId, p.Month, p.Amount, p.PaidDate };
    private static object ExpenseDto(Expense e) => new { e.Id, e.PropertyId, e.Date, e.Category, e.Amount, e.Note };
}

// Request bodies. Numbers and dates arrive as strings or numbers and are checked in the handlers.
public sealed record RenterInput(string? Name, string? Dob);
public sealed record RenterPatch(string? Name, string? Dob, string? Status);
public sealed record PropertyInput(string? Name, string? Address);
public sealed record RoomInput(int PropertyId, string? Name, string? Status, decimal? AskingRent);
public sealed record RoomPatch(string? Status, decimal? AskingRent);
public sealed record TenancyInput(int RoomId, int RenterId, string? StartDate, string? EndDate, decimal? MonthlyRent, int? DueDay);
public sealed record TenancyPatch(string? StartDate, decimal? MonthlyRent, int? DueDay);
public sealed record EndTenancyInput(string? EndDate);
public sealed record ChangeTenantInput(int RenterId, string? StartDate, decimal? MonthlyRent, int? DueDay);
public sealed record PaymentInput(int RenterId, string? Month, decimal? Amount, string? PaidDate);
public sealed record PaymentPatch(string? Month, decimal? Amount, string? PaidDate);
public sealed record ExpenseInput(int PropertyId, string? Date, string? Category, decimal? Amount, string? Note);

// Response shapes for rooms.
// Flat shape the React room map and rooms table read (tenant fields are null for a vacant room).
public sealed record RoomView(
    int Id, int PropertyId, string PropertyName, string Name, string Status, decimal? AskingRent,
    int? TenancyId, int? RenterId, string? Tenant, DateOnly? MovedIn, DateOnly? TenantEnds,
    decimal? Rent, int? DueDay, string? RentState);

public sealed record ReportFacts(
    List<TenancyFact> Tenancies,
    List<PaymentFact> Payments,
    List<ExpenseFact> Expenses,
    List<PropertyName> Properties);
