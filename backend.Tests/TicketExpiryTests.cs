using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TicketExpiry;

namespace backend.Tests
{
    public class TicketExpiryTests : IClassFixture<HubFixture>
    {
        private const string ExpiryReason = "Not resolved within 60 days";
        private readonly HubFixture _hub;

        public TicketExpiryTests(HubFixture hub)
        {
            _hub = hub;
        }

        private TicketExpiryService NewService(bool enabled, double intervalMinutes)
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _hub.ConnectionString,
                ["TicketExpiry:Enabled"] = enabled.ToString(),
                ["TicketExpiry:IntervalMinutes"] = intervalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new TicketExpiryService(configuration, NullLogger<TicketExpiryService>.Instance);
        }

        [Fact]
        public void ServiceIsRegisteredInTheApi()
        {
            Assert.Single(_hub.Services.GetServices<IHostedService>().OfType<TicketExpiryService>());
        }

        [Fact]
        public async Task TicketPastTheDeadlineIsRejectedBySystem()
        {
            var ticket = await _hub.Submit(new CustomerForm());
            _hub.Backdate(ticket.TicketId, 61);

            await NewService(false, 60).RunOnceAsync();

            var after = await _hub.Read(ticket.TicketId);
            Assert.Equal("Rejected", after.TicketStatus);
            Assert.Equal("Reject", after.Decision!.DecisionType);
            Assert.Equal("system", after.Decision.DecidedBy);
            Assert.Equal(ExpiryReason, after.Decision.Reason);
            Assert.Equal("Rejected automatically", _hub.AuditMessage(ticket.TicketId, "Rejected"));
        }

        [Fact]
        public async Task TicketInsideTheDeadlineIsLeftInReview()
        {
            var ticket = await _hub.Submit(new CustomerForm());
            _hub.Backdate(ticket.TicketId, 59);

            await NewService(false, 60).RunOnceAsync();

            var after = await _hub.Read(ticket.TicketId);
            Assert.Equal("Review", after.TicketStatus);
            Assert.Null(after.Decision);
        }

        [Fact]
        public async Task TicketThatAlreadyHasADecisionIsNotTouched()
        {
            var ticket = await _hub.Submit(new CustomerForm());
            await _hub.Reject(_hub.One, ticket.TicketId);
            _hub.Backdate(ticket.TicketId, 61);

            await NewService(false, 60).RunOnceAsync();

            var after = await _hub.Read(ticket.TicketId);
            Assert.Equal("Rejected", after.TicketStatus);
            Assert.Equal(_hub.ReviewerOne, after.Decision!.DecidedBy);
            Assert.Equal(1, _hub.CountRows("Decisions", ticket.TicketId));
        }

        [Fact]
        public async Task RunningTwiceDoesNotDecideTheTicketTwice()
        {
            var ticket = await _hub.Submit(new CustomerForm());
            _hub.Backdate(ticket.TicketId, 61);
            var service = NewService(false, 60);

            await service.RunOnceAsync();
            await service.RunOnceAsync();

            Assert.Equal(1, _hub.CountRows("Decisions", ticket.TicketId));
        }

        [Fact]
        public async Task TimerRejectsExpiredTicketsOnItsOwn()
        {
            using var service = NewService(true, 0.02);
            await service.StartAsync(CancellationToken.None);
            try
            {
                var ticket = await _hub.Submit(new CustomerForm());
                _hub.Backdate(ticket.TicketId, 61);

                var status = "Review";
                for (var i = 0; i < 40 && status == "Review"; i++)
                {
                    await Task.Delay(500);
                    status = (await _hub.Read(ticket.TicketId)).TicketStatus;
                }

                Assert.Equal("Rejected", status);
                Assert.Equal(ExpiryReason, _hub.SystemDecision(ticket.TicketId)!.Value.Reason);
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
            }
        }
    }
}
