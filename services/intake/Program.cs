using Intake.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// EF Core — shared SQL Server database (transitional arrangement).
// Supply the connection string via the ConnectionStrings__DefaultConnection
// environment variable. Never hardcode credentials here.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "'ConnectionStrings:DefaultConnection' is not configured. " +
        "Set the ConnectionStrings__DefaultConnection environment variable.");

builder.Services.AddDbContext<IntakeDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddControllers();

var app = builder.Build();

// Fail fast if the pepper is missing — a missing pepper would silently produce
// wrong TaxId hashes that can never be matched against Identity's hashes.
var pepper = app.Configuration["TaxIdHashPepper"];
if (string.IsNullOrWhiteSpace(pepper))
{
    throw new InvalidOperationException(
        "'TaxIdHashPepper' must be configured. " +
        "Set the TaxIdHashPepper environment variable.");
}

// In Docker the app runs behind a reverse proxy (nginx or the gateway).
// ForwardedHeaders lets it see the real client IP and protocol.
app.UseForwardedHeaders();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Health check: the gateway and docker-compose can poll GET /health to know
// the service is up. Returns 200 OK with a short JSON body.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "intake" }));

app.Run();
