using RentTracker.Core.Rules;

namespace RentTracker.Core.Reports;

public sealed record MonthRow(string Month, decimal Charged, decimal Collected, decimal Arrears, decimal Expenses, decimal Profit);

public sealed record MonthlyReportResult(IReadOnlyList<MonthRow> Months, ReportTotals Totals);

/// Income and expenses month by month. Income is the rent collected in that month.
public static class MonthlyReport
{
    public static MonthlyReportResult Build(
        string from,
        string to,
        IReadOnlyCollection<TenancyFact> tenancies,
        IReadOnlyCollection<PaymentFact> payments,
        IReadOnlyCollection<ExpenseFact> expenses)
    {
        var rows = Months.Range(from[..7], to[..7]).Select(month =>
        {
            var charged = tenancies.Where(t => Tenancies.IsActiveIn(t.Start, t.End, month)).Sum(t => t.Rent);
            var collected = payments.Where(p => p.Month == month).Sum(p => p.Amount);
            var spent = expenses.Where(e => Months.MonthOf(e.Date) == month).Sum(e => e.Amount);
            return new MonthRow(
                month,
                Money.Round2(charged),
                Money.Round2(collected),
                Money.Round2(charged - collected),
                Money.Round2(spent),
                Money.Round2(collected - spent));
        }).ToList();

        return new MonthlyReportResult(rows, SumOf(rows));
    }

    internal static ReportTotals SumOf(IEnumerable<MonthRow> rows)
    {
        var list = rows.ToList();
        return new ReportTotals(
            Money.Round2(list.Sum(r => r.Charged)),
            Money.Round2(list.Sum(r => r.Collected)),
            Money.Round2(list.Sum(r => r.Arrears)),
            Money.Round2(list.Sum(r => r.Expenses)),
            Money.Round2(list.Sum(r => r.Profit)));
    }
}
