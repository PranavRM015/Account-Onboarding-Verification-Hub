using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountSecurity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Accounts
{
    // Only what signing in needs. The customer profile lives in the intake service; CustomerId is the id that service gave them.
    public class CustomerAccount : ILoginAccount
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int CustomerId { get; set; }
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

    public class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccount>
    {
        public void Configure(EntityTypeBuilder<CustomerAccount> builder)
        {
            builder.HasIndex(c => c.Username).IsUnique().HasDatabaseName("UX_CustomerAccounts_Username");
        }
    }
}
