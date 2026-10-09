using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Customers;

namespace Tickets
{
    public static class TicketValues
    {
        public static readonly string[] Statuses = { "Submitted", "FormatVerification", "Review", "Approved", "Rejected" };
        public static readonly string[] AccountTypes = { "Individual", "Joint" };
        public static readonly string[] RiskRatings = { "Low", "Medium", "High" };
    }

    public class Ticket
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ticketId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Customer ID must be positive.")]
        public int customerId { get; set; }
        [ForeignKey(nameof(customerId))]
        public Customer customer { get; set; } = null!;
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string status { get; set; } = "Submitted";
        [MaxLength(15), Column(TypeName = "varchar(15)")]
        public string accountType { get; set; } = string.Empty;
        [MaxLength(6), Column(TypeName = "varchar(6)")]
        public string? riskRating { get; set; } // null until scored
        public DateTime createdAt { get; set; }
        public DateTime verificationDeadline { get; set; }
        [MaxLength(450)]
        public string? assignedTo { get; set; }
        private Ticket() { } // Used by EF Core for materialization

        public Ticket(int customerId, string accountType, string? assignedTo)
        {
            this.customerId = customerId;
            this.status = "Submitted";
            this.accountType = accountType;
            this.riskRating = null;
            this.createdAt = DateTime.UtcNow;
            this.verificationDeadline = this.createdAt.AddDays(60);
            this.assignedTo = assignedTo;
        }
    }
    public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
    {
        public void Configure(EntityTypeBuilder<Ticket> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Status",
                    "status IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
                t.HasCheckConstraint("CK_AccountType",
                    "accountType IN ('Individual', 'Joint')");
                t.HasCheckConstraint("CK_RiskRating",
                    "riskRating IS NULL OR riskRating IN ('Low', 'Medium', 'High')");
                t.HasCheckConstraint("CK_VerificationDeadline",
                    "verificationDeadline = DATEADD(DAY, 60, createdAt)");
            });
            builder.Property(x => x.status).HasDefaultValue("Submitted");
            builder.HasOne(t => t.customer)
                .WithMany()
                .HasForeignKey(t => t.customerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
    public class TicketValidator : AbstractValidator<Ticket>
    {
        public TicketValidator()
        {
            RuleFor(x => x.customerId).GreaterThan(0);

            RuleFor(x => x.status).NotEmpty()
                .Must(v => TicketValues.Statuses.Contains(v))
                .WithMessage("Status must be Submitted, FormatVerification, Review, Approved, or Rejected.");

            RuleFor(x => x.accountType).NotEmpty()
                .Must(v => TicketValues.AccountTypes.Contains(v))
                .WithMessage("Account type must be Individual or Joint.");

            RuleFor(x => x.riskRating)
                .Must(v => TicketValues.RiskRatings.Contains(v!))
                .When(x => x.riskRating != null)
                .WithMessage("Risk rating must be Low, Medium, or High.");

            RuleFor(x => x.verificationDeadline)
                .Equal(x => x.createdAt.AddDays(60))
                .WithMessage("Verification deadline must be 60 days after creation.");

            RuleFor(x => x.assignedTo).MaximumLength(450);
        }
    }
}
