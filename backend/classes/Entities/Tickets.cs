using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Customers;
using Admins;

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
        public int TicketId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Customer ID must be positive.")]
        public int CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public Customer Customer { get; set; } = null!;
        // Second account holder; set only when AccountType is "Joint".
        public int? JointCustomerId { get; set; }
        [ForeignKey(nameof(JointCustomerId))]
        public Customer? JointCustomer { get; set; }
        [MaxLength(20), Column(TypeName = "varchar(20)")]
        public string Status { get; set; } = "Submitted";
        [MaxLength(15), Column(TypeName = "varchar(15)")]
        public string AccountType { get; set; } = string.Empty;
        [MaxLength(6), Column(TypeName = "varchar(6)")]
        public string? RiskRating { get; set; } // null until scored
        public DateTime CreatedAt { get; set; }
        public DateTime VerificationDeadline { get; set; }
        [Required, MaxLength(100)]
        public string AssignedTo { get; set; } = string.Empty; // references Admins.Username
        [ForeignKey(nameof(AssignedTo))]
        public Admin AssignedToAdmin { get; set; } = null!;
        private Ticket() { } // Used by EF Core for materialization

        public Ticket(int customerId, string accountType, string assignedTo, int? jointCustomerId = null)
        {
            this.CustomerId = customerId;
            this.JointCustomerId = jointCustomerId;
            this.Status = "Submitted";
            this.AccountType = accountType;
            this.RiskRating = null;
            this.CreatedAt = DateTime.UtcNow;
            this.VerificationDeadline = this.CreatedAt.AddDays(60);
            this.AssignedTo = assignedTo;
        }
    }
    public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
    {
        public void Configure(EntityTypeBuilder<Ticket> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Status",
                    "Status IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
                t.HasCheckConstraint("CK_AccountType",
                    "AccountType IN ('Individual', 'Joint')");
                t.HasCheckConstraint("CK_RiskRating",
                    "RiskRating IS NULL OR RiskRating IN ('Low', 'Medium', 'High')");
                t.HasCheckConstraint("CK_VerificationDeadline",
                    "VerificationDeadline = DATEADD(DAY, 60, CreatedAt)");
                t.HasCheckConstraint("CK_JointAccount",
                    "(AccountType = 'Joint' AND JointCustomerId IS NOT NULL AND JointCustomerId <> CustomerId) " +
                    "OR (AccountType = 'Individual' AND JointCustomerId IS NULL)");
            });
            builder.Property(x => x.Status).HasDefaultValue("Submitted");
            builder.HasOne(t => t.Customer)
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(t => t.JointCustomer)
                .WithMany()
                .HasForeignKey(t => t.JointCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(t => t.AssignedToAdmin)
                .WithMany()
                .HasForeignKey(t => t.AssignedTo)
                .HasPrincipalKey(a => a.Username)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
    public class TicketValidator : AbstractValidator<Ticket>
    {
        public TicketValidator()
        {
            RuleFor(x => x.CustomerId).GreaterThan(0);

            RuleFor(x => x.JointCustomerId).NotNull().GreaterThan(0)
                .When(x => x.AccountType == "Joint")
                .WithMessage("A joint account requires a second customer.");

            RuleFor(x => x.JointCustomerId).NotEqual(x => x.CustomerId)
                .When(x => x.AccountType == "Joint")
                .WithMessage("The second holder must be a different customer.");

            RuleFor(x => x.JointCustomerId).Null()
                .When(x => x.AccountType == "Individual")
                .WithMessage("An individual account cannot have a second customer.");

            RuleFor(x => x.Status).NotEmpty()
                .Must(v => TicketValues.Statuses.Contains(v))
                .WithMessage("Status must be Submitted, FormatVerification, Review, Approved, or Rejected.");

            RuleFor(x => x.AccountType).NotEmpty()
                .Must(v => TicketValues.AccountTypes.Contains(v))
                .WithMessage("Account type must be Individual or Joint.");

            RuleFor(x => x.RiskRating)
                .Must(v => TicketValues.RiskRatings.Contains(v!))
                .When(x => x.RiskRating != null)
                .WithMessage("Risk rating must be Low, Medium, or High.");

            RuleFor(x => x.VerificationDeadline)
                .Equal(x => x.CreatedAt.AddDays(60))
                .WithMessage("Verification deadline must be 60 days after creation.");

            RuleFor(x => x.AssignedTo)
                .NotEmpty()
                .MaximumLength(100)
                .WithMessage("Assigned to must be specified and not exceed 100 characters.");
        }
    }
}
