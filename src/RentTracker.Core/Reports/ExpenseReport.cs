using RentTracker.Core.Rules;

namespace RentTracker.Core.Reports;

public sealed record CategoryTotal(string Category, decimal Amount);

public sealed record PropertyExpenseTotal(int Id, string Name, decimal Total, IReadOnlyDictionary<string, decimal> ByCategory);

public sealed record ExpenseReportResult(
    decimal Total,
    IReadOnlyList<CategoryTotal> ByCategory,
    IReadOnlyList<PropertyExpenseTotal> ByProperty,
    IReadOnlyList<ExpenseFact> Items);

/// Expenses grouped by category and by property.
public static class ExpenseReport
{
    public static ExpenseReportResult Build(IReadOnlyCollection<ExpenseFact> expenses)
    {
        var items = expenses.OrderBy(e => e.Date).ThenBy(e => e.Id).ToList();

        var total = Money.Round2(items.Sum(e => e.Amount));

        var byCategory = items
            .GroupBy(e => e.Category)
            .Select(g => new CategoryTotal(g.Key, Money.Round2(g.Sum(e => e.Amount))))
            .OrderByDescending(c => c.Amount)
            .ToList();

        var byProperty = items
            .GroupBy(e => e.PropertyId)
            .Select(g => new PropertyExpenseTotal(
                g.Key,
                g.First().PropertyName,
                Money.Round2(g.Sum(e => e.Amount)),
                g.GroupBy(e => e.Category).ToDictionary(c => c.Key, c => Money.Round2(c.Sum(e => e.Amount)))))
            .OrderBy(p => p.Name, StringComparer.CurrentCulture)
            .ToList();

        return new ExpenseReportResult(total, byCategory, byProperty, items);
    }
}
