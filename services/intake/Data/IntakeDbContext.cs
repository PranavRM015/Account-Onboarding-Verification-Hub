using Intake.Models;
using Microsoft.EntityFrameworkCore;

namespace Intake.Data;

public class IntakeDbContext : DbContext
{
    public IntakeDbContext(DbContextOptions<IntakeDbContext> options) : base(options) { }

    public DbSet<CustomerProfile> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.ToTable("Customers");

            // ── Check constraints (must match the existing database) ────────
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Gender",
                    "Gender IN ('Male', 'Female', 'Other')");
                t.HasCheckConstraint("CK_TaxIdType",
                    "TaxIdType IN ('SSN', 'ITIN', 'EIN')");
                t.HasCheckConstraint("CK_TaxIdLastFourDigits",
                    "LEN(TaxIdLastFourDigits) = 4");
                t.HasCheckConstraint("CK_ResidentialAddress",
                    "LEN(ResidentialAddress) > 0");
            });

            // ── Unique indexes (must match the existing database) ───────────
            entity.HasIndex(c => c.TaxIdHash)
                .IsUnique()
                .HasDatabaseName("UX_Customers_TaxIdHash");

            entity.HasIndex(c => c.Username)
                .IsUnique()
                .HasDatabaseName("UX_Customers_Username");

            // ── DB-generated columns — Intake never writes these ────────────

            // CreatedAt is set by the database on INSERT via GETUTCDATE().
            // ValueGeneratedOnAdd tells EF Core to read it back after insert
            // but never include it in INSERT statements.
            entity.Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAdd();

            // Credential columns: the database defaults supply empty strings
            // and zero. EF Core will not include them in INSERT statements
            // issued by Intake. Identity is responsible for setting them.
            entity.Property(c => c.Username)
                .HasDefaultValue(string.Empty);
            entity.Property(c => c.PasswordHash)
                .HasDefaultValue(string.Empty);
            entity.Property(c => c.PasswordRecoveryQuestion)
                .HasDefaultValue(string.Empty);
            entity.Property(c => c.PasswordRecoveryAnswer)
                .HasDefaultValue(string.Empty);
            entity.Property(c => c.FailedLoginCount)
                .HasDefaultValue(0);
        });
    }
}
