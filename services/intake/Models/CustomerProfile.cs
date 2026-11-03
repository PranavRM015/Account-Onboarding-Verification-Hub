using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Intake.Models;

// Maps to the shared Customers table.
// Profile fields are owned by Intake. The credential fields below are mapped so
// that EF Core knows the full table shape — Identity owns those columns and is
// the only service that should write them.
public class CustomerProfile
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int CustomerId { get; set; }

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

    // Raw number is never stored — only the PBKDF2 hash.
    [MaxLength(32), Column(TypeName = "binary(32)")]
    public byte[] TaxIdHash { get; set; } = Array.Empty<byte>();

    // Last four digits for display only.
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

    public DateTime CreatedAt { get; set; }

    // Credential columns — Identity's responsibility. Mapped here only so
    // EF Core generates correct INSERT statements for the shared table.

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
