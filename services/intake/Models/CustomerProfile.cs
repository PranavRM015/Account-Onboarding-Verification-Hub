using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Intake.Models;

// Maps to the shared Customers table.
// Only the profile fields that Intake is responsible for are exposed here.
// The credential columns (Username, PasswordHash, PasswordRecoveryQuestion,
// PasswordRecoveryAnswer, FailedLoginCount, LockoutEnd) are also mapped below
// so that EF Core generates correct SQL for the shared table during the
// transition period — but they are intentionally not included in Intake's
// business logic. Those columns are owned by the Identity service.
public class CustomerProfile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CustomerId { get; set; }

    // ── Profile fields (Intake owns these) ──────────────────────────────────

    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? MiddleName { get; set; }

    [MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Column(TypeName = "date")]
    public DateTime DateOfBirth { get; set; }

    [MaxLength(6), Column(TypeName = "varchar(6)")]
    public string Gender { get; set; } = string.Empty;

    [MaxLength(4), Column(TypeName = "varchar(4)")]
    public string TaxIdType { get; set; } = string.Empty;

    // Stored as 32-byte hash — the raw number is never persisted.
    [MaxLength(32), Column(TypeName = "binary(32)")]
    public byte[] TaxIdHash { get; set; } = Array.Empty<byte>();

    // Last four digits only — used for display in the admin pending screen.
    [MaxLength(4), Column(TypeName = "char(4)")]
    public string TaxIdLastFourDigits { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ResidentialAddress { get; set; } = string.Empty;

    [MaxLength(2), Column(TypeName = "char(2)")]
    public string CountryOfResidence { get; set; } = string.Empty;

    [MaxLength(2), Column(TypeName = "char(2)")]
    public string CountryOfCitizenship { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Occupation { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Employer { get; set; } = string.Empty;

    // Set by the database (GETUTCDATE()); Intake never writes this.
    public DateTime CreatedAt { get; set; }

    // ── Credential fields (Identity service owns these) ──────────────────────
    // These are mapped so EF Core knows the full table shape and does not
    // omit them from INSERT statements (SQL Server default values fill them in).
    // Intake must not read or modify these columns for any business purpose.

    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(100)]
    public string PasswordRecoveryQuestion { get; set; } = string.Empty;

    [MaxLength(100)]
    public string PasswordRecoveryAnswer { get; set; } = string.Empty;

    public int FailedLoginCount { get; set; }

    public DateTime? LockoutEnd { get; set; }
}
