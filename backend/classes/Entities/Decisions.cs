using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;
using Admins;

namespace Decisions
{
    public static class DecisionValues
    {
        public static readonly string[] DecisionTypes = { "Approve", "Deny" };
    }

    public class Decision
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DecisionId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int TicketId { get; set; } // one decision per ticket; cannot be changed once saved
        [ForeignKey(nameof(TicketId))]
        public Ticket Ticket { get; set; } = null!;
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string DecisionType { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string DecidedBy { get; set; } = string.Empty; // references Admins.Username
        [ForeignKey(nameof(DecidedBy))]
        public Admin DecidedByAdmin { get; set; } = null!;
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
        public DateTime DecidedAt { get; set; }

        // Required by EF Core for materialisation.
        private Decision() { }

        public Decision(int ticketId, string decisionType, string decidedBy, string reason)
        {
            this.TicketId = ticketId;
            this.DecisionType = decisionType;
            this.DecidedBy = decidedBy;
            this.Reason = reason;
            this.DecidedAt = DateTime.UtcNow;
        }
    }

    public class DecisionConfiguration : IEntityTypeConfiguration<Decision>
    {
        public void Configure(EntityTypeBuilder<Decision> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_DecisionType",
                    "DecisionType IN ('Approve', 'Deny')");
                t.HasCheckConstraint("CK_DecisionReason",
                    "Reason IS NOT NULL AND LEN(Reason) > 0");
            });

            // One-to-one: a decision references exactly one ticket, and a ticket has at most one decision.
            // EF creates a unique index on Decisions.TicketId for this.
            builder.HasOne(x => x.Ticket)
            .WithOne()
            .HasForeignKey<Decision>(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

            // Once the decision is saved, EF throws if anything tries to change which ticket it points at.
            builder.Property(x => x.TicketId)
            .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);

            builder.HasOne(x => x.DecidedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.DecidedBy)
            .HasPrincipalKey(a => a.Username)
            .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class DecisionValidator : AbstractValidator<Decision>
    {
        public DecisionValidator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);

            RuleFor(x => x.DecisionType).NotEmpty()
                .Must(v => DecisionValues.DecisionTypes.Contains(v))
                .WithMessage("Decision type must be Approve, or Deny.");

            RuleFor(x => x.DecidedBy).NotEmpty().MaximumLength(100);

            RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        }
    }
}
