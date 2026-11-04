using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Intake.Models;

// Maps to the shared Admins table.
// Profile fields are owned by Intake. Identity owns the real login: password hash,
// recovery question and answer, and lockout state. Those columns are mapped here only
// so EF Core knows the full table shape, and are written as empty placeholders.
// Username is the exception: the table has a unique index on it, so Intake stores
// the same value it sends to Identity.
public class AdminProfile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int AdminId { get; set; }

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

    // Raw SSN is never stored — only the PBKDF2 hash, used to find duplicates.
    [MaxLength(32), Column(TypeName = "binary(32)")]
    public byte[] SSNHash { get; set; } = new byte[32];

    // Set by the database (GETUTCDATE()), never by the signup form.
    public DateTime DateHired { get; set; }

    public bool IsCitizen { get; set; }

    [MaxLength(2)]
    public string CountryOfCitizenship { get; set; } = string.Empty;

    // Login columns — Identity's responsibility.

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
