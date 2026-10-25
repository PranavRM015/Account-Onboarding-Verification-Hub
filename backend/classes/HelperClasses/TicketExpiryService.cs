using Microsoft.Data.SqlClient;

namespace TicketExpiry
{
    public class TicketExpiryService : BackgroundService
    {
        private readonly string _connectionString;
        private readonly bool _enabled;
        private readonly TimeSpan _interval;
        private readonly ILogger<TicketExpiryService> _logger;

        public TicketExpiryService(IConfiguration configuration, ILogger<TicketExpiryService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
            _enabled = configuration.GetValue("TicketExpiry:Enabled", true);
            var minutes = configuration.GetValue("TicketExpiry:IntervalMinutes", 60.0);
            _interval = TimeSpan.FromMinutes(minutes > 0 ? minutes : 60.0);
            _logger = logger;
        }

        public async Task RunOnceAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            using var command = new SqlCommand("pr_expireReviewTickets", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure,
            };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                return;
            }
            using var timer = new PeriodicTimer(_interval);
            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to expire tickets that were not resolved within 60 days.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
