using Data;
using Microsoft.EntityFrameworkCore;
using Review.Api.Authentication;
using Review.Api.Services;

namespace Review.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            string connection = builder.Configuration.GetConnectionString("ReviewConnection")
                ?? throw new InvalidOperationException("Connection string 'ReviewConnection' is required.");
            builder.Services.AddDbContext<ReviewDbContext>(options => options.UseSqlServer(connection));
            builder.Services.AddIdentityAuthentication(builder.Configuration);
            builder.Services.AddControllers();
            builder.Services.AddHubSwagger("Review API");
            builder.Services.AddProblemDetails();
            builder.Services.AddHostedService<TicketExpiryService>();
            var app = builder.Build();
            if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
                await db.Database.MigrateAsync();
            }
            app.UseExceptionHandler();
            app.UseHubSwagger();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            app.MapGet("/health/ready", async (ReviewDbContext db, CancellationToken cancellation) =>
            {
                try
                {
                    await db.Tickets.AsNoTracking().Select(t => t.TicketId).Take(1).ToListAsync(cancellation);
                    return Results.Ok(new { status = "ok" });
                }
                catch { return Results.StatusCode(503); }
            });
            await app.RunAsync();
        }
    }
}
