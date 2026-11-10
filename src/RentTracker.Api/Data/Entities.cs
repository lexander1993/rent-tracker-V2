namespace RentTracker.Api.Data;

// Database tables. Money is decimal(10,2) and dates are DateOnly, which PostgreSQL stores as "date".

public class Renter
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly? Dob { get; set; }
    public string Status { get; set; } = "CURRENT";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Property
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Address { get; set; }
}

public class Room
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Status { get; set; } = "AVAILABLE";
    public decimal? AskingRent { get; set; }
}

public class Tenancy
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int RenterId { get; set; }
    public Renter Renter { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public int DueDay { get; set; } = 1;
}

public class Payment
{
    public int Id { get; set; }
    public int RenterId { get; set; }
    public Renter Renter { get; set; } = null!;
    public string Month { get; set; } = "";
    public decimal Amount { get; set; }
    public DateOnly PaidDate { get; set; }
}

public class Expense
{
    public int Id { get; set; }
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string Category { get; set; } = "Other";
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
