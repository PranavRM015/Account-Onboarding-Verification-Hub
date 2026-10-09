using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;

namespace VendorResponse
{
    public static class VendorResponseValues
    {
        public static readonly string[] MatchResults = { "Match", "Partial", "NoMatch" };
    }

    public class VendorResponse
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int vendorResponseId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int ticketId { get; set; }
        [ForeignKey(nameof(ticketId))]
        public Ticket ticket { get; set; } = null!;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string nameMatch { get; set; } = string.Empty;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string dateOfBirthMatch { get; set; } = string.Empty;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string addressMatch { get; set; } = string.Empty;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string taxIdMatch { get; set; } = string.Empty;
        [Range(0, 50)]
        public int score { get; set; }
        [MaxLength(200)]
        public string riskCodes { get; set; } = string.Empty; // ex. DeceasedSSN, CorrectionalAddress
        public DateTime timeStamp { get; set; }
        private VendorResponse() { } // Used by EF Core for materialization

        public VendorResponse(int ticketId, string nameMatch, string dateOfBirthMatch, string addressMatch, string taxIdMatch, int score, string riskCodes)
        {
            this.ticketId = ticketId;
            this.nameMatch = nameMatch;
            this.dateOfBirthMatch = dateOfBirthMatch;
            this.addressMatch = addressMatch;
            this.taxIdMatch = taxIdMatch;
            this.score = score;
            this.riskCodes = riskCodes;
            this.timeStamp = DateTime.UtcNow;
        }
    }

    public class VendorResponseConfiguration : IEntityTypeConfiguration<VendorResponse>
    {
        public void Configure(EntityTypeBuilder<VendorResponse> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_NameMatch", "nameMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_DateOfBirthMatch", "dateOfBirthMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_AddressMatch", "addressMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_TaxIdMatch", "taxIdMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_Score", "score BETWEEN 0 AND 50");
            });
        }
    }

    public class VendorResponseValidator : AbstractValidator<VendorResponse>
    {
        public VendorResponseValidator()
        {
            RuleFor(x => x.ticketId).GreaterThan(0);
            RuleFor(x => x.nameMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Name match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.dateOfBirthMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Date of birth match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.addressMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Address match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.taxIdMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Tax ID match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.score).InclusiveBetween(0, 50);
            RuleFor(x => x.riskCodes).NotNull().MaximumLength(200);
        }
    }
}
