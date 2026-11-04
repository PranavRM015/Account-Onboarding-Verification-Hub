using Risk.Api.Services;
using Risk.Api.Authentication;

namespace Risk.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddProblemDetails();
            builder.Services.AddIdentityAuthentication(builder.Configuration);
            builder.Services.AddSingleton<RiskAnalyzer>();

            var app = builder.Build();
            // Load the screening files before accepting requests. Missing files must not yield a clean assessment.
            app.Services.GetRequiredService<RiskAnalyzer>();
            app.UseExceptionHandler();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            app.MapControllers();
            app.Run();
        }
    }
}
