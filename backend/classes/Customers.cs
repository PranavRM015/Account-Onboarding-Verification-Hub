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
        public int customerId { get; set; }
        [MaxLength(50)]
        public string firstName { get; set; } = string.Empty;
        [MaxLength(50)]
        public string? middleName { get; set; }
        [MaxLength(50)]
        public string lastName { get; set; } = string.Empty;
        [Column(TypeName = "date")]
        public DateTime dateOfBirth { get; set; }
        [MaxLength(6), Column(TypeName = "varchar(6)")]
        public string gender { get; set; } = string.Empty;
        [MaxLength(4), Column(TypeName = "varchar(4)")]
        public string taxIdType { get; set; } = string.Empty;
        [MaxLength(32), Column(TypeName = "binary(32)")]
        public byte[] taxIdHash { get; set; } = Array.Empty<byte>(); // used to look up duplicates without storing the full number
        [MaxLength(4), Column(TypeName = "char(4)")]
        public string taxIdLastFourDigits { get; set; } = string.Empty; // display only
        [MaxLength(200)]
        public string residentialAddress { get; set; } = string.Empty;
        [MaxLength(2), Column(TypeName = "char(2)")]
        public string countryOfResidence { get; set; } = string.Empty; // ISO country code; will need to be stored in a separate list of countryISO codes
        [MaxLength(2), Column(TypeName = "char(2)")]
        public string countryOfCitizenship { get; set; } = string.Empty; // same as above
        [MaxLength(100)]
        public string occupation { get; set; } = string.Empty;
        [MaxLength(100)]
        public string employer { get; set; } = string.Empty;
    }
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder){
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Gender", "gender IN ('Male', 'Female', 'Other')");
                t.HasCheckConstraint("CK_TaxIdType", "taxIdType IN ('SSN', 'ITIN', 'EIN')");
                t.HasCheckConstraint("CK_TaxIdLastFourDigits", "LEN(taxIdLastFourDigits) = 4");
                t.HasCheckConstraint("CK_ResidentialAddress", "LEN(residentialAddress) > 0");
            });
        }
    }

    public class CustomerValidator : AbstractValidator<Customer>
    {
        private static readonly string[] Genders = { "Male", "Female", "Other" };
        private static readonly string[] TaxIdTypes = { "SSN", "ITIN", "EIN" };

        public CustomerValidator()
        {
            RuleFor(x => x.firstName).NotEmpty().MaximumLength(50);
            RuleFor(x => x.middleName).MaximumLength(50);
            RuleFor(x => x.lastName).NotEmpty().MaximumLength(50);
            RuleFor(x => x.dateOfBirth).LessThan(_ => DateTime.UtcNow.Date)
                .WithMessage("Date of birth must be in the past.");
            RuleFor(x => x.gender).NotEmpty()
                .Must(v => Genders.Contains(v))
                .WithMessage("Gender must be Male, Female, or Other.");
            RuleFor(x => x.taxIdType).NotEmpty()
                .Must(v => TaxIdTypes.Contains(v))
                .WithMessage("Tax ID type must be SSN, ITIN, or EIN.");
            RuleFor(x => x.taxIdHash).NotNull().Must(v => v.Length == 32)
                .WithMessage("Tax ID hash must be 32 bytes.");
            RuleFor(x => x.taxIdLastFourDigits).NotEmpty().Matches(@"^\d{4}$")
                .WithMessage("Tax ID last four digits must be exactly 4 digits.");
            RuleFor(x => x.residentialAddress).NotEmpty().MaximumLength(200);
            RuleFor(x => x.countryOfResidence).NotEmpty().Length(2);
            RuleFor(x => x.countryOfCitizenship).NotEmpty().Length(2);
            RuleFor(x => x.occupation).NotEmpty().MaximumLength(100);
            RuleFor(x => x.employer).NotEmpty().MaximumLength(100);
        }
    }
}
