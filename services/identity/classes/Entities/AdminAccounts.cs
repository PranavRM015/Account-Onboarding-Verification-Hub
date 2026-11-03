using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountSecurity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounts
{
    // Only what signing in needs. The admin profile lives in the intake service; AdminId is the id that service gave them.
    public class AdminAccount : ILoginAccount
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int AdminId { get; set; }
        [Required, MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        [Required, MaxLength(200)]
        public string PasswordHash { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string PasswordRecoveryQuestion { get; set; } = string.Empty;
        [Required, MaxLength(100)]
        public string PasswordRecoveryAnswer { get; set; } = string.Empty; // salted hash of the answer
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }

    public class AdminAccountConfiguration : IEntityTypeConfiguration<AdminAccount>
    {
        public void Configure(EntityTypeBuilder<AdminAccount> builder)
        {
            builder.HasIndex(a => a.Username).IsUnique().HasDatabaseName("UX_AdminAccounts_Username");
        }
    }
}
