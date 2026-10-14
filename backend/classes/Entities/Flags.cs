using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tickets;
using Admins;
namespace Flags
{
    public static class FlagValues
    {
        public static readonly string[] ViolationTypes = { "VerificationFailed", "OfacMatch", "ProhibitedIndustry", "ClosedJurisdiction" };
    }

    public class Flag
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int FlagId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Ticket ID must be positive.")]
        public int TicketId { get; set; }
        [ForeignKey(nameof(TicketId))]
        public Ticket Ticket { get; set; } = null!;
        [MaxLength(30), Column(TypeName = "varchar(30)")]
        public string ViolationType { get; set; } = string.Empty;
        public int? RaisedBy { get; set; } // references Admins.AdminId; null when raised by the system
        [ForeignKey(nameof(RaisedBy))]
        public Admin? RaisedByAdmin { get; set; }
        public DateTime RaisedAt { get; set; }
        public int? ClearedBy { get; set; } // references Admins.AdminId
        [ForeignKey(nameof(ClearedBy))]
        public Admin? ClearedByAdmin { get; set; }
        public DateTime? ClearedAt { get; set; }
        [MaxLength(500)]
        public string? ClearedReason { get; set; } // required when the flag is cleared
        private Flag() { } // used by EF Core for materialization
        public Flag(int ticketId, string violationType, int? raisedBy)
        {
            this.TicketId = ticketId;
            this.ViolationType = violationType;
            this.RaisedBy = raisedBy;
            this.RaisedAt = DateTime.UtcNow;
        }
    }

    public class FlagConfiguration : IEntityTypeConfiguration<Flag>
    {
        public void Configure(EntityTypeBuilder<Flag> builder)
        {
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ViolationType","ViolationType IN ('VerificationFailed', 'OfacMatch', 'ProhibitedIndustry', 'ClosedJurisdiction')");
            });
            builder.HasOne(x => x.Ticket)
            .WithMany()
            .HasForeignKey(x => x.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.RaisedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.RaisedBy)
            .HasPrincipalKey(a => a.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ClearedByAdmin)
            .WithMany()
            .HasForeignKey(x => x.ClearedBy)
            .HasPrincipalKey(a => a.AdminId)
            .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class FlagValidator : AbstractValidator<Flag>
    {
        public FlagValidator()
        {
            RuleFor(x => x.TicketId).GreaterThan(0);
            RuleFor(x => x.ViolationType).NotEmpty()
                .Must(v => FlagValues.ViolationTypes.Contains(v))
                .WithMessage("Invalid violation type.");
            RuleFor(x => x.ClearedReason).MaximumLength(500);
            RuleFor(x => x.ClearedBy).NotEmpty()
                .When(x => x.ClearedAt != null)
                .WithMessage("A cleared flag must record who cleared it.");
            RuleFor(x => x.ClearedReason).NotEmpty()
                .When(x => x.ClearedAt != null)
                .WithMessage("A reason is required when a flag is cleared.");
        }
    }
}
