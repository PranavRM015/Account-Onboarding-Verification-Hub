using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;

namespace VendorResponses
{
    public static class VendorResponseValues
    {
        public static readonly string[] MatchResults = { "Match", "Partial", "NoMatch" };
    }

    public class VendorResponse
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VendorResponseId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int TicketId { get; set; }
        [ForeignKey(nameof(TicketId))]
        public Ticket Ticket { get; set; } = null!;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string NameMatch { get; set; } = string.Empty;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string DateOfBirthMatch { get; set; } = string.Empty;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string AddressMatch { get; set; } = string.Empty;
        [MaxLength(7), Column(TypeName = "varchar(7)")]
        public string TaxIdMatch { get; set; } = string.Empty;
        [Range(0, 50)]
        public int Score { get; set; }
        [MaxLength(200)]
        public string RiskCodes { get; set; } = string.Empty; // ex. DeceasedSSN, CorrectionalAddress
        public DateTime TimeStamp { get; set; }
        private VendorResponse() { } // Used by EF Core for materialization

        public VendorResponse(int ticketId, string nameMatch, string dateOfBirthMatch, string addressMatch, string taxIdMatch, int score, string riskCodes)
        {
            this.TicketId = ticketId;
            this.NameMatch = nameMatch;
            this.DateOfBirthMatch = dateOfBirthMatch;
            this.AddressMatch = addressMatch;
            this.TaxIdMatch = taxIdMatch;
            this.Score = score;
            this.RiskCodes = riskCodes;
            this.TimeStamp = DateTime.UtcNow;
        }
    }

    public class VendorResponseConfiguration : IEntityTypeConfiguration<VendorResponse>
    {
        public void Configure(EntityTypeBuilder<VendorResponse> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_NameMatch", "NameMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_DateOfBirthMatch", "DateOfBirthMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_AddressMatch", "AddressMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_TaxIdMatch", "TaxIdMatch IN ('Match', 'Partial', 'NoMatch')");
                t.HasCheckConstraint("CK_Score", "Score BETWEEN 0 AND 50");
            });
            builder.HasOne(x => x.Ticket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class VendorResponseValidator : AbstractValidator<VendorResponse>
    {
        public VendorResponseValidator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.NameMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Name match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.DateOfBirthMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Date of birth match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.AddressMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Address match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.TaxIdMatch).NotEmpty()
                .Must(v => VendorResponseValues.MatchResults.Contains(v))
                .WithMessage("Tax ID match must be Match, Partial, or NoMatch.");

            RuleFor(x => x.Score).InclusiveBetween(0, 50);
            RuleFor(x => x.RiskCodes).NotNull().MaximumLength(200);
        }
    }
}
