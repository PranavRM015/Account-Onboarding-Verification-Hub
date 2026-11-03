using System;
using System.ComponentModel.DataAnnotations;

namespace RevokedTokens
{
    // A JWT that was logged out before it expired. The row can be deleted once ExpiresAt has passed.
    public class RevokedToken
    {
        [Key, MaxLength(64)]
        public string Jti { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }

        // Required by EF Core for materialization.
        private RevokedToken() { }

        public RevokedToken(string jti, DateTime expiresAt)
        {
            this.Jti = jti;
            this.ExpiresAt = expiresAt;
        }
    }
}
