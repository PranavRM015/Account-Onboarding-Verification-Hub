using Intake.Data;
using Intake.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "'ConnectionStrings:DefaultConnection' is not configured.");

builder.Services.AddDbContext<IntakeDbContext>(options =>
    options.UseSqlServer(connectionString));

var identityBaseUrl = builder.Configuration["Identity:BaseUrl"] ?? "http://localhost:5290";
builder.Services.AddHttpClient<IdentityServiceClient>(client =>
{
    client.BaseAddress = new Uri(identityBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// JWT validation using Identity's RS256 public key from its JWKS endpoint.
// Role names are lowercase ("admin", "customer") as defined by Identity.
// MapInboundClaims = false is required so "role" is not remapped by the JWT middleware.
//
// INTEGRATION NOTE: Intake fetches Identity's public key at startup via JWKS.
// If Identity is not running, Intake will still start but tokens will be rejected
// because the signing key cannot be fetched. Set Identity:BaseUrl to point to the
// running Identity service instance.
//
// LIMITATION: Intake cannot check the RevokedTokens table (that lives in Identity's DB).
// A logged-out token will still pass validation here until it expires naturally.
// This is acceptable for a P3 project and is the same trade-off described in CONTRACT.md.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identityBaseUrl;
        options.MetadataAddress = $"{identityBaseUrl}/.well-known/jwks.json";
        options.RequireHttpsMetadata = false; // HTTP is fine inside the Docker network
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = "AccountVerificationOnboardingHub",
            ValidateAudience = true,
            ValidAudience = "AccountVerificationOnboardingHub",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();

var app = builder.Build();

// Fail fast if the pepper is missing — a wrong pepper produces TaxId hashes
// that will never match what Identity computed, silently breaking duplicate detection.
var pepper = app.Configuration["TaxIdHashPepper"];
if (string.IsNullOrWhiteSpace(pepper))
    throw new InvalidOperationException("'TaxIdHashPepper' is not configured.");

app.UseForwardedHeaders();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "intake" }));

app.Run();
