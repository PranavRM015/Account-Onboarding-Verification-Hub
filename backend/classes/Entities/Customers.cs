using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Customers{
    public class Customer
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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
        [MaxLength(32), Column(TypeName = "binary(32)")]
        public byte[] TaxIdHash { get; set; } = Array.Empty<byte>(); // used to look up duplicates without storing the full number
        [MaxLength(4), Column(TypeName = "char(4)")]
        public string TaxIdLastFourDigits { get; set; } = string.Empty; // display only
        [MaxLength(200)]
        public string ResidentialAddress { get; set; } = string.Empty;
        [MaxLength(2), Column(TypeName = "char(2)")]
        public string CountryOfResidence { get; set; } = string.Empty; // ISO country code; will need to be stored in a separate list of countryISO codes
        [MaxLength(2), Column(TypeName = "char(2)")]
        public string CountryOfCitizenship { get; set; } = string.Empty; // same as above
        [MaxLength(100)]
        public string Occupation { get; set; } = string.Empty;
        [MaxLength(100)]
        public string Employer { get; set; } = string.Empty;
    }
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder){
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Gender", "Gender IN ('Male', 'Female', 'Other')");
                t.HasCheckConstraint("CK_TaxIdType", "TaxIdType IN ('SSN', 'ITIN', 'EIN')");
                t.HasCheckConstraint("CK_TaxIdLastFourDigits", "LEN(TaxIdLastFourDigits) = 4");
                t.HasCheckConstraint("CK_ResidentialAddress", "LEN(ResidentialAddress) > 0");
            });
            // One customer record per tax ID; the hash is what is compared, never the raw number.
            builder.HasIndex(c => c.TaxIdHash)
                .IsUnique()
                .HasDatabaseName("UX_Customers_TaxIdHash");
        }
    }

    public class CustomerValidator : AbstractValidator<Customer>
    {
        private static readonly string[] Genders = { "Male", "Female", "Other" };
        private static readonly string[] TaxIdTypes = { "SSN", "ITIN", "EIN" };

        public CustomerValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
            RuleFor(x => x.MiddleName).MaximumLength(50);
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
            RuleFor(x => x.DateOfBirth).LessThan(_ => DateTime.UtcNow.Date)
                .WithMessage("Date of birth must be in the past.");
            RuleFor(x => x.Gender).NotEmpty()
                .Must(v => Genders.Contains(v))
                .WithMessage("Gender must be Male, Female, or Other.");
            RuleFor(x => x.TaxIdType).NotEmpty()
                .Must(v => TaxIdTypes.Contains(v))
                .WithMessage("Tax ID type must be SSN, ITIN, or EIN.");
            RuleFor(x => x.TaxIdHash).NotNull().Must(v => v.Length == 32)
                .WithMessage("Tax ID hash must be 32 bytes.");
            RuleFor(x => x.TaxIdLastFourDigits).NotEmpty().Matches(@"^\d{4}$")
                .WithMessage("Tax ID last four digits must be exactly 4 digits.");
            RuleFor(x => x.ResidentialAddress).NotEmpty().MaximumLength(200);
            RuleFor(x => x.CountryOfResidence).NotEmpty().Length(2);
            RuleFor(x => x.CountryOfCitizenship).NotEmpty().Length(2);
            RuleFor(x => x.Occupation).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Employer).NotEmpty().MaximumLength(100);
        }
    }
}
