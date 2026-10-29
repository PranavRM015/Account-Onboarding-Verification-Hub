using System;
using AccountSecurity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Admins
{
    public class Admin : ILoginAccount
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AdminId { get; set; }
        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string PasswordHash { get; set; } = string.Empty;
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;
        [MaxLength(50)]
        public string? MiddleName { get; set; }
        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;
        [Column(TypeName = "date")]
        public DateTime DateOfBirth { get; set; }
        [Required, MaxLength(6), Column(TypeName = "varchar(6)")]
        public string Gender { get; set; } = string.Empty;
        [MaxLength(32), Column(TypeName = "binary(32)")]
        public byte[] SSNHash { get; set; } = new byte[32]; // used to look up duplicates without storing the full number
        // Set by the database (GETUTCDATE()), never by the signup form.
        public DateTime DateHired { get; set; }
        public bool IsCitizen { get; set; }
        [Required, MaxLength(2)]
        public string CountryOfCitizenship { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string PasswordRecoveryQuestion { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string PasswordRecoveryAnswer { get; set; } = string.Empty; // salted hash of the answer
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEnd { get; set; }

        private Admin() { }
        public Admin(string username, string passwordHash)
        {
            this.Username = username;
            this.PasswordHash = passwordHash;
        }
    }
    public class AdminConfiguration : IEntityTypeConfiguration<Admin>
    {
        public void Configure(EntityTypeBuilder<Admin> builder)
        {
            builder.HasAlternateKey(a => a.Username);
            builder.Property(a => a.DateHired).HasDefaultValueSql("GETUTCDATE()").ValueGeneratedOnAdd();
            builder.HasIndex(a => a.SSNHash).IsUnique().HasDatabaseName("UX_Admins_SSNHash");
            builder.ToTable(t => t.HasCheckConstraint("CK_Admins_Gender", "Gender IN ('Male', 'Female', 'Other')"));
        }
    }
    public class AdminValidator : AbstractValidator<Admin>
    {
        public AdminValidator()
        {
            RuleFor(x => x.Username)
                .NotEmpty()
                .WithMessage("Username is required.")
                .MaximumLength(100)
                .WithMessage("Username cannot exceed 100 characters.");

            RuleFor(x => x.PasswordHash)
                .NotEmpty()
                .WithMessage("Password hash is required.")
                .MaximumLength(200)
                .WithMessage("Password hash cannot exceed 200 characters.");

            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
            RuleFor(x => x.MiddleName).MaximumLength(50);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
            RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(_ => DateTime.UtcNow.Date.AddYears(-18))
                .WithMessage("Admin must be at least 18 years old.");
            RuleFor(x => x.Gender).Must(v => v is "Male" or "Female" or "Other")
                .WithMessage("Gender must be Male, Female, or Other.");
            RuleFor(x => x.SSNHash).NotNull().Must(v => v.Length == 32)
                .WithMessage("SSN hash must be 32 bytes.");
            RuleFor(x => x.CountryOfCitizenship).Must(v => CountryCodes.All.Contains(v))
                .WithMessage("Country of citizenship must be a valid two-letter country code.");
            RuleFor(x => x.PasswordRecoveryQuestion).Must(RecoveryQuestions.IsValid)
                .WithMessage("Choose one of the password recovery questions.");
            RuleFor(x => x.PasswordRecoveryAnswer).NotEmpty().MaximumLength(100);
        }
    }
}
