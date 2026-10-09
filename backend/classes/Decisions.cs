using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;

namespace Decisions
{
    public static class DecisionValues
    {
        public static readonly string[] DecisionTypes = { "Approve", "SecondApproval", "Restrict", "Deny" };
    }

    public class Decision
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int decisionId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int ticketId { get; set; }
        [ForeignKey(nameof(ticketId))]
        public Ticket ticket { get; set; } = null!;
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string decisionType { get; set; } = string.Empty;
        [MaxLength(450)]
        public string decidedBy { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string reason { get; set; } = string.Empty;
        public DateTime decidedAt { get; set; }

        // Required by EF Core for materialisation.
        private Decision() { }

        public Decision(int ticketId, string decisionType, string decidedBy, string reason)
        {
            this.ticketId = ticketId;
            this.decisionType = decisionType;
            this.decidedBy = decidedBy;
            this.reason = reason;
            this.decidedAt = DateTime.UtcNow;
        }
    }

    public class DecisionConfiguration : IEntityTypeConfiguration<Decision>
    {
        public void Configure(EntityTypeBuilder<Decision> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_DecisionType",
                    "decisionType IN ('Approve', 'SecondApproval', 'Restrict', 'Deny')");
                t.HasCheckConstraint("CK_DecisionReason",
                    "reason IS NOT NULL AND LEN(reason) > 0");
            });
        }
    }

    public class DecisionValidator : AbstractValidator<Decision>
    {
        public DecisionValidator()
        {
            RuleFor(x => x.ticketId).GreaterThan(0);

            RuleFor(x => x.decisionType).NotEmpty()
                .Must(v => DecisionValues.DecisionTypes.Contains(v))
                .WithMessage("Decision type must be Approve, SecondApproval, Restrict, or Deny.");

            RuleFor(x => x.decidedBy).NotEmpty().MaximumLength(450);

            RuleFor(x => x.reason).NotEmpty().MaximumLength(1000);
        }
    }
}
