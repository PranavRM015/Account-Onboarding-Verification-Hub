using Accounts;
using AccountSecurity;
using Data;
using JwtTokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PasswordHasherNamespace;
using System.Threading.RateLimiting;

public class Program
{

    public static void Main(string[] args)
    {
        Console.WriteLine("Starting the identity service...");
        var builder = WebApplication.CreateBuilder(args);
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200" };
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("MyCorsPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        // Behind Azure App Service the app sees plain http; this makes it trust the X-Forwarded-* headers so it knows the original request was https.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityConnection")));

        // JWT bearer tokens are the only sign-in. APIs answer 401/403 instead of redirecting to a login page.
        builder.Services.AddSingleton<JwtTokenService>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new JwtTokenService(builder.Configuration).ValidationParameters();
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    // A token that was logged out is rejected even though it has not expired yet.
                    OnTokenValidated = async ctx =>
                    {
                        var jti = ctx.Principal?.FindFirst("jti")?.Value;
                        var db = ctx.HttpContext.RequestServices.GetRequiredService<IdentityDbContext>();
                        if (string.IsNullOrEmpty(jti) || await db.RevokedTokens.AnyAsync(t => t.Jti == jti))
                        {
                            ctx.Fail("This token has been revoked.");
                        }
                    },
                };
            });
        builder.Services.AddAuthorization();

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

        JwtTokenService.SigningKey(app.Configuration); // fails at startup if the private key is missing or too weak

        var internalKey = app.Configuration["Internal:ApiKey"];
        if (string.IsNullOrWhiteSpace(internalKey) || internalKey.Length < 32)
        {
            throw new InvalidOperationException("'Internal:ApiKey' must be set to a secret of at least 32 characters.");
        }
        if (!app.Environment.IsDevelopment() && internalKey.StartsWith("dev-only"))
        {
            throw new InvalidOperationException("'Internal:ApiKey' must not be the development value in production.");
        }

        // Off by default. Docker Compose turns it on so a brand new database gets its tables; elsewhere run `dotnet ef database update`.
        if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.Migrate();
        }

        CreateFirstAdmin(app);

        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseCors("MyCorsPolicy");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        // The other services fetch this to check the tokens this service signs.
        app.MapGet("/.well-known/jwks.json", (JwtTokenService tokens) => Results.Json(tokens.PublicKeySet()));
        // JwtBearer in the other services starts here (Authority = this service's URL) and follows jwks_uri to the keys.
        app.MapGet("/.well-known/openid-configuration", (HttpContext http, JwtTokenService tokens) =>
            Results.Json(tokens.DiscoveryDocument($"{http.Request.Scheme}://{http.Request.Host}")));
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        app.Run();
    }

    // A brand new database can get its first admin from BootstrapAdmin:Username and BootstrapAdmin:Password.
    // BootstrapAdmin:AdminId (default 1) must match the profile the intake service holds for that admin.
    private static void CreateFirstAdmin(WebApplication app)
    {
        var username = app.Configuration["BootstrapAdmin:Username"]?.Trim();
        var password = app.Configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return;
        }
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            if (db.AdminAccounts.Any())
            {
                return;
            }
            db.AdminAccounts.Add(new AdminAccount
            {
                AdminId = app.Configuration.GetValue("BootstrapAdmin:AdminId", 1),
                Username = username,
                PasswordHash = PasswordHasher.PasswordHash(password),
                PasswordRecoveryQuestion = RecoveryQuestions.All[0],
                PasswordRecoveryAnswer = "!", // never matches, so recovery is off until the admin sets a real answer
            });
            db.SaveChanges();
            app.Logger.LogInformation("Created the first admin '{Username}'.", username);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Could not create the first admin. Check that the migrations have been applied.");
        }
    }
}
