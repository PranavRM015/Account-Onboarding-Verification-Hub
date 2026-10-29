using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace JwtTokens
{
    public class JwtTokenService
    {
        // Tokens carry the role (Admin or User) under this claim name; controllers check it with [Authorize(Roles = ...)].
        public const string RoleClaim = "role";
        private readonly SymmetricSecurityKey _key;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly TimeSpan _lifetime;

        public JwtTokenService(IConfiguration configuration)
        {
            _key = SigningKey(configuration);
            _issuer = configuration["Jwt:Issuer"] ?? "AccountVerificationOnboardingHub";
            _audience = configuration["Jwt:Audience"] ?? "AccountVerificationOnboardingHub";
            var minutes = configuration.GetValue("Jwt:ExpiryMinutes", 60.0);
            _lifetime = TimeSpan.FromMinutes(minutes > 0 ? minutes : 60.0);
        }

        public static SymmetricSecurityKey SigningKey(IConfiguration configuration)
        {
            var key = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            {
                throw new InvalidOperationException("'Jwt:Key' must be set to a secret of at least 32 characters.");
            }
            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        }

        public TokenValidationParameters ValidationParameters() => new()
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Name,
            RoleClaimType = RoleClaim,
        };

        // Every token gets a unique jti so that logout can revoke exactly this one.
        public (string Token, DateTime ExpiresAt) Issue(string username, string role)
        {
            var expiresAt = DateTime.UtcNow.Add(_lifetime);
            var token = new JwtSecurityToken(
                _issuer,
                _audience,
                new[]
                {
                    new Claim(JwtRegisteredClaimNames.Name, username),
                    new Claim(RoleClaim, role),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                },
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
