using RentTracker.Core.Reports;
using Xunit;

namespace RentTracker.Core.Tests;

public class MonthlyReportTests
{
    [Fact]
    public void Counts_rent_for_each_active_month_and_nets_off_expenses()
    {
        var tenancies = new[] { new TenancyFact(1, 1, new DateOnly(2026, 8, 1), null, 1000m) };
        var payments = new[] { new PaymentFact(1, "2026-08", 1000m) };
        var expenses = new[] { new ExpenseFact(1, 1, "Gravity Residence", new DateOnly(2026, 8, 5), "Utilities", "", 200m) };

        var result = MonthlyReport.Build("2026-08", "2026-09", tenancies, payments, expenses);

        var aug = result.Months[0];
        Assert.Equal(1000m, aug.Charged);
        Assert.Equal(1000m, aug.Collected);
        Assert.Equal(0m, aug.Arrears);
        Assert.Equal(200m, aug.Expenses);
        Assert.Equal(800m, aug.Profit);

        var sep = result.Months[1];
        Assert.Equal(1000m, sep.Charged);
        Assert.Equal(0m, sep.Collected);
        Assert.Equal(1000m, sep.Arrears);

        Assert.Equal(2000m, result.Totals.Charged);
        Assert.Equal(1000m, result.Totals.Arrears);
    }
}

public class ProfitReportTests
{
    [Fact]
    public void Profit_is_rent_collected_minus_expenses_per_property()
    {
        var properties = new[] { new PropertyName(1, "Gravity Residence") };
        var tenancies = new[] { new TenancyFact(1, 1, new DateOnly(2026, 8, 1), null, 1000m) };
        var payments = new[] { new PaymentFact(1, "2026-08", 500m) };
        var expenses = new[] { new ExpenseFact(1, 1, "Gravity Residence", new DateOnly(2026, 8, 5), "Utilities", "", 200m) };

        var result = ProfitReport.Build("2026-08", "2026-08", properties, tenancies, payments, expenses);

        var row = Assert.Single(result.Properties);
        Assert.Equal("Gravity Residence", row.Name);
        Assert.Equal(1000m, row.Charged);
        Assert.Equal(500m, row.Collected);
        Assert.Equal(500m, row.Arrears);
        Assert.Equal(200m, row.Expenses);
        Assert.Equal(300m, row.Profit);
    }
}

public class ExpenseReportTests
{
    [Fact]
    public void Totals_by_category_and_property()
    {
        var expenses = new[]
        {
            new ExpenseFact(1, 1, "Gravity Residence", new DateOnly(2026, 8, 1), "Utilities", "", 100m),
            new ExpenseFact(2, 1, "Gravity Residence", new DateOnly(2026, 8, 2), "Utilities", "", 50m),
            new ExpenseFact(3, 2, "Orchid Residence", new DateOnly(2026, 8, 3), "Cleaning", "", 30m),
        };

        var result = ExpenseReport.Build(expenses);

        Assert.Equal(180m, result.Total);
        Assert.Equal("Utilities", result.ByCategory[0].Category);
        Assert.Equal(150m, result.ByCategory[0].Amount);
        Assert.Equal(2, result.ByProperty.Count);
    }
}

public class CsvTests
{
    [Fact]
    public void Every_cell_is_quoted_and_quotes_are_doubled() =>
        Assert.Equal("\"He said \"\"hi\"\"\",\"2\"", Csv.Line("He said \"hi\"", 2));

    [Fact]
    public void Lines_are_joined_with_CRLF() =>
        Assert.Equal("\"a\"\r\n\"b\"", Csv.Document(new[] { "\"a\"", "\"b\"" }));
}
