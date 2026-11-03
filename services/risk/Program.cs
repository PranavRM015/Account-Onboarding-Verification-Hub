using Risk.Api.Services;

namespace Risk.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // This prototype has no authentication yet; listen only on the local machine.
            builder.WebHost.UseUrls("http://127.0.0.1:5291");
            builder.Services.AddControllers();
            builder.Services.AddSingleton<RiskAnalyzer>();

            var app = builder.Build();
            // Load the screening files before accepting requests. Missing files must not yield a clean assessment.
            app.Services.GetRequiredService<RiskAnalyzer>();
            app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
            app.MapControllers();
            app.Run();
        }
    }
}
