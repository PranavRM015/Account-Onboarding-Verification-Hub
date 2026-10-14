using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;
using Customers;

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
        public int RecId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int TicketId { get; set; }
        [ForeignKey(nameof(TicketId))]
        public Ticket Ticket { get; set; } = null!;
        [Range(1, int.MaxValue, ErrorMessage = "Customer ID must be positive.")]
        public int CustomerId { get; set; } // should match Ticket.CustomerId
        [ForeignKey(nameof(CustomerId))]
        public Customer Customer { get; set; } = null!;
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string PrevStatus { get; set; } = string.Empty;
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string CurrStatus { get; set; } = string.Empty;
        [MaxLength(250)]
        public string Message { get; set; } = string.Empty;
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
        public DateTime DateTimeOccurred { get; set; }
        private AuditTrail() { } // Used by EF Core for materialization.
        public AuditTrail(int ticketId, int customerId, string prevStatus, string currStatus, string message, string reason)
        {
            this.TicketId = ticketId;
            this.CustomerId = customerId;
            this.PrevStatus = prevStatus;
            this.CurrStatus = currStatus;
            this.Message = message;
            this.Reason = reason;
            this.DateTimeOccurred = DateTime.UtcNow;
        }
    }

    public class AuditTrailConfiguration : IEntityTypeConfiguration<AuditTrail>
    {
        public void Configure(EntityTypeBuilder<AuditTrail> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_AuditPrevStatus",
                    "PrevStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
                t.HasCheckConstraint("CK_AuditCurrStatus",
                    "CurrStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
            });
            builder.HasOne(x => x.Ticket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class AuditTrailValidator : AbstractValidator<AuditTrail>
    {
        public AuditTrailValidator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);

            RuleFor(x => x.CustomerId).GreaterThan(0);

            RuleFor(x => x.PrevStatus).NotEmpty()
                .Must(v => AuditTrailValues.Statuses.Contains(v))
                .WithMessage("Invalid previous status.");

            RuleFor(x => x.CurrStatus).NotEmpty()
                .Must(v => AuditTrailValues.Statuses.Contains(v))
                .WithMessage("Invalid current status.");

            RuleFor(x => x.Message).NotEmpty().MaximumLength(250);

            RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        }
    }
}
