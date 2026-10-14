using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Admins;
using PasswordHasher;
using Data;
using AdminControllers;
using Microsoft.AspNetCore.Mvc;
using AdminRepo;

public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Starting the application...");
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("MyCorsPolicy", policy =>
            {
                policy.WithOrigins("http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials(); // Use only if frontend passes cookies/tokens
            });
        });

        builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddControllers();
        var app = builder.Build();
        app.UseCors("MyCorsPolicy");
        app.MapControllers();
        app.UseRouting();        
        app.MapPost("/api/users", (AdminInformation info) =>
        {
            var username = info.username?.Trim() ?? string.Empty;
            var password = info.password ?? string.Empty;

            if (username.Length == 0 || username.Length > 100 || password.Length == 0)
            {
                return Results.BadRequest("A username (max 100 characters) and a password are required.");
            }
            try
            {
                var adminRepository = new AdminRepository(builder.Configuration.GetConnectionString("DefaultConnection"));
                adminRepository.AddAdmin(username, password);
                return Results.Ok("created");
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // Unique constraint violation
            {
                return Results.Conflict("That username is already taken.");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Failed to create admin '{Username}'.", username);
                return Results.Problem("Could not create the admin account.");
            }
        });

        
        app.Run();
    }
}