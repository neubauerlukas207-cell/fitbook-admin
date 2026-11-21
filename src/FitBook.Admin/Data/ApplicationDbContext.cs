using FitBook.Admin.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FitBook.Admin.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<MembershipPlan> Plans => Set<MembershipPlan>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // tabulky Identity

        builder.Entity<MembershipPlan>(e =>
        {
            e.Property(p => p.Price).HasPrecision(10, 2);
            e.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
        });

        builder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
        });

        builder.Entity<Membership>(e =>
        {
            e.Property(m => m.PricePaid).HasPrecision(10, 2);
            e.HasOne(m => m.Plan).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Customer).WithMany(c => c.Memberships).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => new { m.CustomerId, m.ValidTo });
        });

        builder.Entity<CheckIn>(e =>
        {
            e.HasOne(c => c.Customer).WithMany(c => c.CheckIns).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Membership).WithMany().OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => new { c.CustomerId, c.AtUtc });
        });
    }
}
