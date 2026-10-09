using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;

namespace AuditTrails
{
    public static class AuditTrailValues
    {
        public static readonly string[] Statuses = { "Submitted", "FormatVerification", "Review", "Approved", "Rejected" };
    }

    // Rows in AuditTrail are never updated or deleted.
    public class AuditTrail
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int recId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int ticketId { get; set; }
        [ForeignKey(nameof(ticketId))]
        public Ticket ticket { get; set; } = null!;
        [MaxLength(450)]
        public string userId { get; set; } = string.Empty;
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string prevStatus { get; set; } = string.Empty;
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string currStatus { get; set; } = string.Empty;
        [MaxLength(250)]
        public string message { get; set; } = string.Empty;
        [MaxLength(500)]
        public string reason { get; set; } = string.Empty;
        public DateTime dateTimeOccurred { get; set; }
        private AuditTrail() { } // Used by EF Core for materialization.
        public AuditTrail(int ticketId, string userId, string prevStatus, string currStatus, string message, string reason)
        {
            this.ticketId = ticketId;
            this.userId = userId;
            this.prevStatus = prevStatus;
            this.currStatus = currStatus;
            this.message = message;
            this.reason = reason;
            this.dateTimeOccurred = DateTime.UtcNow;
        }
    }

    public class AuditTrailConfiguration : IEntityTypeConfiguration<AuditTrail>
    {
        public void Configure(EntityTypeBuilder<AuditTrail> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AuditPrevStatus",
                    "prevStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
                t.HasCheckConstraint("CK_AuditCurrStatus",
                    "currStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
            });
            builder.HasOne(x => x.ticket)
            .WithMany()
            .HasForeignKey(x => x.ticketId)
            .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class AuditTrailValidator : AbstractValidator<AuditTrail>
    {
        public AuditTrailValidator()
        {
            RuleFor(x => x.ticketId).GreaterThan(0);

            RuleFor(x => x.userId).NotEmpty().MaximumLength(450);

            RuleFor(x => x.prevStatus).NotEmpty()
                .Must(v => AuditTrailValues.Statuses.Contains(v))
                .WithMessage("Invalid previous status.");

            RuleFor(x => x.currStatus).NotEmpty()
                .Must(v => AuditTrailValues.Statuses.Contains(v))
                .WithMessage("Invalid current status.");

            RuleFor(x => x.message).NotEmpty().MaximumLength(250);

            RuleFor(x => x.reason).NotEmpty().MaximumLength(500);
        }
    }
}
