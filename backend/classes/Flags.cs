using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;
namespace Flags
{
    public static class FlagValues
    {
        public static readonly string[] ViolationTypes = { "VerificationFailed", "OfacMatch", "ProhibitedIndustry", "ClosedJurisdiction" };
    }

    public class Flag
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int flagId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int ticketId { get; set; }
        [ForeignKey(nameof(ticketId))]
        public Ticket ticket { get; set; } = null!;
        [MaxLength(30), Column(TypeName = "varchar(30)")]
        public string violationType { get; set; } = string.Empty;
        [MaxLength(450)]
        public string? raisedBy { get; set; } // references AspNetUsers; null when raised by the system
        public DateTime raisedAt { get; set; }
        [MaxLength(450)]
        public string? clearedBy { get; set; }
        public DateTime? clearedAt { get; set; }
        [MaxLength(500)]
        public string? clearedReason { get; set; } // required when the flag is cleared
        private Flag() { } // used by EF Core for materialization
        public Flag(int ticketId, string violationType, string? raisedBy)
        {
            this.ticketId = ticketId;
            this.violationType = violationType;
            this.raisedBy = raisedBy;
            this.raisedAt = DateTime.UtcNow;
        }
    }

    public class FlagConfiguration : IEntityTypeConfiguration<Flag>
    {
        public void Configure(EntityTypeBuilder<Flag> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ViolationType","violationType IN ('VerificationFailed', 'OfacMatch', 'ProhibitedIndustry', 'ClosedJurisdiction')");
                t.HasCheckConstraint("CK_ClearedReason","clearedAt IS NULL OR (clearedBy IS NOT NULL AND clearedReason IS NOT NULL AND LEN(clearedReason) > 0)");
            });
            builder.HasOne(x => x.ticket)
            .WithMany()
            .HasForeignKey(x => x.ticketId)
            .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class FlagValidator : AbstractValidator<Flag>
    {
        public FlagValidator()
        {
            RuleFor(x => x.ticketId).GreaterThan(0);
            RuleFor(x => x.violationType).NotEmpty()
                .Must(v => FlagValues.ViolationTypes.Contains(v))
                .WithMessage("Invalid violation type.");
            RuleFor(x => x.raisedBy).MaximumLength(450);
            RuleFor(x => x.clearedBy).MaximumLength(450);
            RuleFor(x => x.clearedReason).MaximumLength(500);
            RuleFor(x => x.clearedBy).NotEmpty()
                .When(x => x.clearedAt != null)
                .WithMessage("A cleared flag must record who cleared it.");
            RuleFor(x => x.clearedReason).NotEmpty()
                .When(x => x.clearedAt != null)
                .WithMessage("A reason is required when a flag is cleared.");
        }
    }
}
