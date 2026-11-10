namespace RentTracker.Core.Reports;

// Plain copies of the database rows that the reports need. The reports never touch the database,
// so they can be tested on their own.

public sealed record TenancyFact(int PropertyId, int RenterId, DateOnly Start, DateOnly? End, decimal Rent);

public sealed record PaymentFact(int RenterId, string Month, decimal Amount);

public sealed record ExpenseFact(int Id, int PropertyId, string PropertyName, DateOnly Date, string Category, string Note, decimal Amount);

public sealed record PropertyName(int Id, string Name);

public sealed record ReportTotals(decimal Charged, decimal Collected, decimal Arrears, decimal Expenses, decimal Profit);
