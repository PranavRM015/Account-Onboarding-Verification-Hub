using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Admins;
using AdminControlNamespace;
using AdminRepo;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
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

        // Cookie sign-in for admins; APIs answer 401/403 instead of redirecting to a login page.
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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

        builder.Services.AddHostedService<TicketExpiryService>();

        builder.Services.AddControllers();
        var app = builder.Build();

        var pepper = app.Configuration["TaxIdHashPepper"];
        if (string.IsNullOrWhiteSpace(pepper)
            || (!app.Environment.IsDevelopment() && (pepper.Length < 32 || pepper == "dev-only-change-me-before-production")))
        {
            throw new InvalidOperationException("'TaxIdHashPepper' must be set to a strong value of at least 32 characters.");
        }

        CreateFirstAdmin(app);

        app.UseForwardedHeaders();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors("MyCorsPolicy");
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
