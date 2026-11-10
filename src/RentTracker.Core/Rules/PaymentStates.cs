namespace RentTracker.Core.Rules;

public enum PaymentState
{
    Paid,
    Partial,
    NotPaid,
}

public static class PaymentStates
{
    public static PaymentState For(decimal due, decimal paid) =>
        paid >= due ? PaymentState.Paid : paid > 0 ? PaymentState.Partial : PaymentState.NotPaid;

    /// The labels the React app shows: PAID, PARTIAL or NOT PAID.
    public static string ToLabel(PaymentState state) => state switch
    {
        PaymentState.Paid => "PAID",
        PaymentState.Partial => "PARTIAL",
        _ => "NOT PAID",
    };
}
