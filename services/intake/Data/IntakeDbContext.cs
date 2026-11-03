using Intake.Models;
using Microsoft.EntityFrameworkCore;

namespace Intake.Data;

public class IntakeDbContext : DbContext
{
    public IntakeDbContext(DbContextOptions<IntakeDbContext> options) : base(options) { }

    public DbSet<CustomerProfile> Customers { get; set; }

    // Read-only view of the Tickets table. Intake queries this to check whether
    // a customer has already been onboarded. Intake never inserts into Tickets.
    public DbSet<TicketReadModel> Tickets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.ToTable("Customers");

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Gender",        "Gender IN ('Male', 'Female', 'Other')");
                t.HasCheckConstraint("CK_TaxIdType",     "TaxIdType IN ('SSN', 'ITIN', 'EIN')");
                t.HasCheckConstraint("CK_TaxIdLastFourDigits", "LEN(TaxIdLastFourDigits) = 4");
                t.HasCheckConstraint("CK_ResidentialAddress",  "LEN(ResidentialAddress) > 0");
            });

            entity.HasIndex(c => c.TaxIdHash).IsUnique().HasDatabaseName("UX_Customers_TaxIdHash");
            entity.HasIndex(c => c.Username).IsUnique().HasDatabaseName("UX_Customers_Username");

            // DB sets CreatedAt on INSERT; EF Core reads it back but never writes it.
            entity.Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();

            // Credential columns default to '' in the database. HasDefaultValue tells
            // EF Core to omit them from INSERT statements — Identity writes these.
            entity.Property(c => c.Username).HasDefaultValue(string.Empty);
            entity.Property(c => c.PasswordHash).HasDefaultValue(string.Empty);
            entity.Property(c => c.PasswordRecoveryQuestion).HasDefaultValue(string.Empty);
            entity.Property(c => c.PasswordRecoveryAnswer).HasDefaultValue(string.Empty);
            entity.Property(c => c.FailedLoginCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<TicketReadModel>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(t => t.TicketId);
        });
    }
}
