using Intake.Authentication;
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

// JWT validation. Intake fetches Identity's public signing keys straight from its JWKS endpoint, exactly as Risk and
// Review do. It used to go through Identity's OpenID discovery document, which builds the key address from the incoming
// request; behind Azure's internal ingress that address was unreachable, so every authenticated call hung for the
// 60-second backchannel timeout and then failed with 401.
//
// The address defaults to Identity:BaseUrl + /.well-known/jwks.json, so no extra setting is needed. Issuer and audience
// come from appsettings.json (Identity:Issuer / Identity:Audience).
//
// LIMITATION: Intake cannot check the RevokedTokens table (that lives in Identity's DB).
// A logged-out token will still pass validation here until it expires naturally.
// This is acceptable for a P3 project and is the same trade-off described in CONTRACT.md.
var jwksUrl = builder.Configuration["Identity:JwksUrl"] ?? identityBaseUrl.TrimEnd('/') + "/.well-known/jwks.json";
builder.Configuration["Identity:JwksUrl"] = jwksUrl;
builder.Configuration["Identity:RequireHttps"] ??= jwksUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase).ToString();
builder.Services.AddIdentityAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

// Signup is open to anyone, so it is limited per IP the same way Identity limits login.
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
builder.Services.AddHubSwagger("Intake API");

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
app.UseHubSwagger();
app.UseCors("MyCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "intake" }));

app.Run();
