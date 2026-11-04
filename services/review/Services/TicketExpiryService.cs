using Data;
using Microsoft.EntityFrameworkCore;

namespace Review.Api.Services
{
    public class TicketExpiryService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TicketExpiryService> _logger;

        public TicketExpiryService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<TicketExpiryService> logger)
        {
            _scopes = scopes;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_configuration.GetValue("TicketExpiry:Enabled", true)) { return; }
            double minutes = _configuration.GetValue("TicketExpiry:IntervalMinutes", 60.0);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes > 0 ? minutes : 60));
            do
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
                    await db.Database.ExecuteSqlRawAsync("EXEC dbo.pr_expireReviewTickets", stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { _logger.LogError(ex, "Failed to expire overdue review tickets."); }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
