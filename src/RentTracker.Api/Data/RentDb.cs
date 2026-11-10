using Microsoft.EntityFrameworkCore;

namespace RentTracker.Api.Data;

public class RentDb(DbContextOptions<RentDb> options) : DbContext(options)
{
    public DbSet<Renter> Renters => Set<Renter>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Tenancy> Tenancies => Set<Tenancy>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Renter>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(200);
            e.Property(r => r.Status).HasMaxLength(20);
        });

        model.Entity<Property>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Address).HasMaxLength(300);
        });

        model.Entity<Room>(e =>
        {
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Status).HasMaxLength(20);
            e.Property(r => r.AskingRent).HasPrecision(10, 2);
            e.HasIndex(r => new { r.PropertyId, r.Name }).IsUnique();
            e.HasOne(r => r.Property).WithMany().HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Tenancy>(e =>
        {
            e.Property(t => t.MonthlyRent).HasPrecision(10, 2);
            e.HasIndex(t => t.RoomId);
            e.HasIndex(t => t.RenterId);
            e.HasOne(t => t.Room).WithMany().HasForeignKey(t => t.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.Renter).WithMany().HasForeignKey(t => t.RenterId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(10, 2);
            e.Property(p => p.Month).HasMaxLength(7);
            e.HasIndex(p => new { p.RenterId, p.Month });
            e.HasOne(p => p.Renter).WithMany().HasForeignKey(p => p.RenterId).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.Property(x => x.Category).HasMaxLength(60);
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
