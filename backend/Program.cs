using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Admins;
using AdminControlNamespace;
using Microsoft.AspNetCore.Mvc;

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

        app.Run();
    }
}