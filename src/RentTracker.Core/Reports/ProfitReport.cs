using RentTracker.Core.Rules;

namespace RentTracker.Core.Reports;

public sealed record PropertyProfit(int Id, string Name, decimal Charged, decimal Collected, decimal Arrears, decimal Expenses, decimal Profit);

public sealed record ProfitReportResult(IReadOnlyList<PropertyProfit> Properties, ReportTotals Totals);

/// Profit = rent collected minus expenses (cash basis), by property.
/// Rent is counted by calendar month: a tenancy counts for every month it covers within the range.
public static class ProfitReport
{
    /// Payments from a tenant who is not in a room during that month go here.
    public const int UnlinkedId = 0;
    public const string UnlinkedName = "Not linked to a room";

    public static ProfitReportResult Build(
        string from,
        string to,
        IReadOnlyCollection<PropertyName> properties,
        IReadOnlyCollection<TenancyFact> tenancies,
        IReadOnlyCollection<PaymentFact> payments,
        IReadOnlyCollection<ExpenseFact> expenses)
    {
        var months = Months.Range(from[..7], to[..7]);
        var charged = new Dictionary<int, decimal>();
        var collected = new Dictionary<int, decimal>();
        var spent = new Dictionary<int, decimal>();
        var names = properties.ToDictionary(p => p.Id, p => p.Name);

        foreach (var tenancy in tenancies)
        {
            foreach (var month in months)
            {
                if (Tenancies.IsActiveIn(tenancy.Start, tenancy.End, month))
                {
                    Add(charged, tenancy.PropertyId, tenancy.Rent);
                }
            }
        }

        foreach (var payment in payments)
        {
            var current = tenancies.FirstOrDefault(t =>
                t.RenterId == payment.RenterId && Tenancies.IsActiveIn(t.Start, t.End, payment.Month));
            Add(collected, current?.PropertyId ?? UnlinkedId, payment.Amount);
        }

        foreach (var expense in expenses)
        {
            Add(spent, expense.PropertyId, expense.Amount);
        }

        // Properties first (in the order given), then anything else that has money against it.
        var order = properties.Select(p => p.Id)
            .Concat(new[] { UnlinkedId })
            .Concat(charged.Keys).Concat(collected.Keys).Concat(spent.Keys)
            .Distinct()
            .ToList();

        var rows = new List<PropertyProfit>();
        foreach (var id in order)
        {
            var c = charged.GetValueOrDefault(id);
            var col = collected.GetValueOrDefault(id);
            var ex = spent.GetValueOrDefault(id);
            if (c == 0 && col == 0 && ex == 0)
            {
                continue;
            }

            var name = names.TryGetValue(id, out var known) ? known : UnlinkedName;
            rows.Add(new PropertyProfit(
                id,
                name,
                Money.Round2(c),
                Money.Round2(col),
                Money.Round2(c - col),
                Money.Round2(ex),
                Money.Round2(col - ex)));
        }

        return new ProfitReportResult(rows, Totals(rows));
    }

    private static void Add(Dictionary<int, decimal> totals, int key, decimal amount) =>
        totals[key] = totals.GetValueOrDefault(key) + amount;

    private static ReportTotals Totals(IReadOnlyCollection<PropertyProfit> rows) =>
        new(
            Money.Round2(rows.Sum(r => r.Charged)),
            Money.Round2(rows.Sum(r => r.Collected)),
            Money.Round2(rows.Sum(r => r.Arrears)),
            Money.Round2(rows.Sum(r => r.Expenses)),
            Money.Round2(rows.Sum(r => r.Profit)));
}
