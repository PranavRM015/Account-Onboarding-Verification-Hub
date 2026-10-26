using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Admins;
using AdminControlNamespace;
using AdminRepo;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using JwtTokens;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using System.Threading.RateLimiting;
using TicketExpiry;

public class Program
{

    public static void Main(string[] args)
    {
        Console.WriteLine("Starting the application...");
        var builder = WebApplication.CreateBuilder(args);
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:4200" };
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("MyCorsPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // Use only if frontend passes cookies/tokens
            });
        });

        // Behind Azure App Service the app sees plain http; this makes it trust the X-Forwarded-* headers so it knows the original request was https.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // JWT bearer tokens are the main sign-in. The cookie still works as a fallback when no Authorization header is sent.
        // APIs answer 401/403 instead of redirecting to a login page.
        builder.Services.AddSingleton<JwtTokenService>();
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "BearerOrCookie";
                options.DefaultChallengeScheme = "BearerOrCookie";
            })
            .AddPolicyScheme("BearerOrCookie", "Bearer or cookie", options =>
            {
                options.ForwardDefaultSelector = ctx =>
                    ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
            })
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
                        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        if (string.IsNullOrEmpty(jti) || await db.RevokedTokens.AnyAsync(t => t.Jti == jti))
                        {
                            ctx.Fail("This token has been revoked.");
                        }
                    },
                };
            })
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            });
        builder.Services.AddAuthorization();

        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("AccountVerificationOnboardingHub");
        var keysPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

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

        builder.Services.AddHostedService<TicketExpiryService>();

        builder.Services.AddControllers();
        var app = builder.Build();

        var pepper = app.Configuration["TaxIdHashPepper"];
        if (string.IsNullOrWhiteSpace(pepper)
            || (!app.Environment.IsDevelopment() && (pepper.Length < 32 || pepper == "dev-only-change-me-before-production")))
        {
            throw new InvalidOperationException("'TaxIdHashPepper' must be set to a strong value of at least 32 characters.");
        }

        var jwtKey = app.Configuration["Jwt:Key"];
        JwtTokenService.SigningKey(app.Configuration); // fails at startup if the key is missing or too short
        if (!app.Environment.IsDevelopment() && jwtKey!.StartsWith("dev-only"))
        {
            throw new InvalidOperationException("'Jwt:Key' must not be the development value in production.");
        }

        CreateFirstAdmin(app);

        app.UseForwardedHeaders();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors("MyCorsPolicy");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapFallbackToFile("{*path:regex(^(?!api/).*$)}", "index.html");

        app.Run();
    }

    // A brand new database can get its first admin from BootstrapAdmin:Username and BootstrapAdmin:Password.
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
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (db.Admins.Any())
            {
                return;
            }
            var connectionString = app.Configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            new AdminRepository(connectionString).AddAdmin(username, password);
            app.Logger.LogInformation("Created the first admin '{Username}'.", username);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Could not create the first admin. Check that the migrations have been applied.");
        }
    }
}
