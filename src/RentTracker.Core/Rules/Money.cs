namespace RentTracker.Core.Rules;

public static class Money
{
    /// Rounds to cents, halves away from zero (the same result as the old JavaScript API).
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
