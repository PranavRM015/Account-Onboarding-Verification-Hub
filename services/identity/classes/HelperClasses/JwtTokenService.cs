using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AccountSecurity;
using Microsoft.IdentityModel.Tokens;

namespace JwtTokens
{
    // Signs tokens with the private key; every other service checks them with the public key from /.well-known/jwks.json.
    public class JwtTokenService
    {
        // Claim names every service reads. A token carries "role" plus "adminId" or "customerId", whichever matches the role.
        public const string RoleClaim = "role";
        public const string AdminIdClaim = "adminId";
        public const string CustomerIdClaim = "customerId";
        private readonly RsaSecurityKey _key;
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

        public static RsaSecurityKey SigningKey(IConfiguration configuration)
        {
            var pem = configuration["Jwt:PrivateKeyPem"];
            if (string.IsNullOrWhiteSpace(pem))
            {
                throw new InvalidOperationException("'Jwt:PrivateKeyPem' must be set to an RSA private key in PEM format.");
            }
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048)
            {
                throw new InvalidOperationException("'Jwt:PrivateKeyPem' must be an RSA key of at least 2048 bits.");
            }
            // The key id lets the other services pick the right key out of the published set when the key is replaced.
            var keyId = Base64UrlEncoder.Encode(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
            return new RsaSecurityKey(rsa) { KeyId = keyId };
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

        // The public half only, in the JSON Web Key Set format.
        public object PublicKeySet()
        {
            var pub = _key.Rsa.ExportParameters(false);
            return new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "RSA",
                        use = "sig",
                        alg = SecurityAlgorithms.RsaSha256,
                        kid = _key.KeyId,
                        n = Base64UrlEncoder.Encode(pub.Modulus),
                        e = Base64UrlEncoder.Encode(pub.Exponent),
                    },
                },
            };
        }

        // Every token gets a unique jti so that logout can revoke exactly this one.
        public (string Token, DateTime ExpiresAt) Issue(string username, string role, int accountId)
        {
            var expiresAt = DateTime.UtcNow.Add(_lifetime);
            var token = new JwtSecurityToken(
                _issuer,
                _audience,
                new[]
                {
                    new Claim(JwtRegisteredClaimNames.Name, username),
                    new Claim(RoleClaim, role),
                    new Claim(role == Roles.Admin ? AdminIdClaim : CustomerIdClaim, accountId.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                },
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.RsaSha256));
            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
