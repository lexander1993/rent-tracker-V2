using RentTracker.Core.Rules;
using Xunit;

namespace RentTracker.Core.Tests;

public class MonthsTests
{
    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("2026-13", false)]
    [InlineData("2026-1", false)]
    [InlineData(null, false)]
    public void IsMonth_accepts_only_yyyy_mm(string? value, bool expected) =>
        Assert.Equal(expected, Months.IsMonth(value));

    [Fact]
    public void Index_round_trips_through_FromIndex() =>
        Assert.Equal("2026-10", Months.FromIndex(Months.Index("2026-10")));

    [Fact]
    public void Range_is_inclusive_across_a_year_end() =>
        Assert.Equal(new[] { "2025-11", "2025-12", "2026-01" }, Months.Range("2025-11", "2026-01").ToArray());

    [Fact]
    public void Trailing_returns_oldest_first() =>
        Assert.Equal(new[] { "2026-08", "2026-09", "2026-10" }, Months.Trailing("2026-10", 3).ToArray());

    [Fact]
    public void DueDate_uses_the_due_day_in_that_month() =>
        Assert.Equal(new DateOnly(2028, 2, 28), Months.DueDate("2028-02", 28));
}

public class TenanciesTests
{
    [Fact]
    public void Tenancy_is_active_for_any_month_it_touches()
    {
        var start = new DateOnly(2026, 10, 15);
        Assert.True(Tenancies.IsActiveIn(start, null, "2026-10"));
        Assert.True(Tenancies.IsActiveIn(start, null, "2027-03"));
        Assert.False(Tenancies.IsActiveIn(start, null, "2026-09"));
    }

    [Fact]
    public void Tenancy_ending_on_the_first_still_counts_for_that_month() =>
        Assert.True(Tenancies.IsActiveIn(new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 1), "2026-10"));

    [Fact]
    public void Tenancy_ending_on_the_day_another_starts_does_not_clash()
    {
        var existingStart = new DateOnly(2026, 1, 1);
        var existingEnd = new DateOnly(2026, 3, 1);
        Assert.False(Tenancies.Overlaps(existingStart, existingEnd, new DateOnly(2026, 3, 1), null));
    }

    [Fact]
    public void Open_ended_tenancy_clashes_with_a_later_start() =>
        Assert.True(Tenancies.Overlaps(new DateOnly(2026, 1, 1), null, new DateOnly(2026, 6, 1), null));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(28, true)]
    [InlineData(29, false)]
    public void Due_day_must_be_between_1_and_28(int dueDay, bool expected) =>
        Assert.Equal(expected, Tenancies.IsDueDayValid(dueDay));
}

public class RoomRentStatesTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);

    [Fact]
    public void Tenant_who_starts_later_is_upcoming() =>
        Assert.Equal("UPCOMING", RoomRentStates.Compute(new DateOnly(2026, 10, 20), 1000m, 0m, 1, Today, "2026-10"));

    [Fact]
    public void Paid_in_full_is_paid() =>
        Assert.Equal("PAID", RoomRentStates.Compute(new DateOnly(2026, 1, 1), 1000m, 1000m, 15, Today, "2026-10"));

    [Fact]
    public void Due_within_a_week_is_due_soon() =>
        Assert.Equal("DUE_SOON", RoomRentStates.Compute(new DateOnly(2026, 1, 1), 1000m, 0m, 15, Today, "2026-10"));

    [Fact]
    public void Due_day_already_passed_is_overdue() =>
        Assert.Equal("OVERDUE", RoomRentStates.Compute(new DateOnly(2026, 1, 1), 1000m, 0m, 5, Today, "2026-10"));

    [Fact]
    public void Due_more_than_a_week_away_is_open() =>
        Assert.Equal("OPEN", RoomRentStates.Compute(new DateOnly(2026, 1, 1), 1000m, 0m, 28, Today, "2026-10"));
}

public class PaymentStatesTests
{
    [Fact]
    public void Full_payment_is_paid() => Assert.Equal(PaymentState.Paid, PaymentStates.For(1000m, 1000m));

    [Fact]
    public void Some_payment_is_partial() => Assert.Equal(PaymentState.Partial, PaymentStates.For(1000m, 400m));

    [Fact]
    public void No_payment_is_not_paid() => Assert.Equal(PaymentState.NotPaid, PaymentStates.For(1000m, 0m));

    [Fact]
    public void Labels_match_the_React_app() =>
        Assert.Equal("NOT PAID", PaymentStates.ToLabel(PaymentState.NotPaid));
}

public class MoneyTests
{
    [Theory]
    [InlineData(1.005, 1.01)]
    [InlineData(2.5, 2.5)]
    [InlineData(-1.005, -1.01)]
    public void Round2_halves_away_from_zero(double input, double expected) =>
        Assert.Equal((decimal)expected, Money.Round2((decimal)input));
}
