using Intake.Data;
using Intake.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

var riskBaseUrl = builder.Configuration["Risk:BaseUrl"] ?? "http://localhost:5291";
builder.Services.AddHttpClient<RiskServiceClient>(client =>
{
    client.BaseAddress = new Uri(riskBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

var reviewBaseUrl = builder.Configuration["Review:BaseUrl"] ?? "http://localhost:5292";
builder.Services.AddHttpClient<ReviewServiceClient>(client =>
{
    client.BaseAddress = new Uri(reviewBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
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

// Rate limiting for the public signup endpoint, matching the team's pattern.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
    {
        var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = configuration.GetValue("RateLimiting:Auth:PermitLimit", 10),
                Window = TimeSpan.FromSeconds(configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60)),
                QueueLimit = 0,
            });
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// Fail fast if required secrets are missing or use the placeholder values.
var pepper = app.Configuration["TaxIdHashPepper"];
if (string.IsNullOrWhiteSpace(pepper) || pepper.StartsWith("REPLACE-"))
    throw new InvalidOperationException("'TaxIdHashPepper' must be set to a real secret value.");

var internalKey = app.Configuration["Internal:ApiKey"];
if (string.IsNullOrWhiteSpace(internalKey) || internalKey.Length < 32 || internalKey.StartsWith("REPLACE-"))
    throw new InvalidOperationException("'Internal:ApiKey' must be set to a secret of at least 32 characters.");
if (!app.Environment.IsDevelopment() && internalKey.StartsWith("dev-only"))
    throw new InvalidOperationException("'Internal:ApiKey' must not be the development value in production.");

// When Docker Compose sets Database:EnsureCreatedOnStartup=true, this creates the
// Intake database schema (VerificationHub_Intake) on first run. Off by default so
// running against the shared monolith database does not alter its schema.
if (app.Configuration.GetValue("Database:EnsureCreatedOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<IntakeDbContext>().Database.EnsureCreated();
}

app.UseForwardedHeaders();
app.UseRouting();
app.UseCors("MyCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "intake" }));

app.Run();
