using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using TicketDtos;

namespace backend.Tests
{
    // Two reviewers acting on the same ticket at the same moment: the second must be turned away, not let through.
    public class TicketLockTests : IClassFixture<HubFixture>
    {
        private readonly HubFixture _hub;

        public TicketLockTests(HubFixture hub)
        {
            _hub = hub;
        }

        // Stands in for "another reviewer is mid-request": holds the ticket's row lock, exactly as the first reviewer's request does.
        private async Task<IAsyncDisposable> HoldLock(int ticketId)
        {
            var connection = new SqlConnection(_hub.ConnectionString);
            await connection.OpenAsync();
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            using (var command = new SqlCommand("SELECT TicketId FROM Tickets WITH (UPDLOCK, ROWLOCK) WHERE TicketId = @id", connection, transaction))
            {
                command.Parameters.AddWithValue("@id", ticketId);
                await command.ExecuteScalarAsync();
            }
            return new Held(connection, transaction);
        }

        private sealed class Held(SqlConnection connection, SqlTransaction transaction) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                await transaction.RollbackAsync();
                await connection.DisposeAsync();
            }
        }

        private Task<TicketDetailsDto> NewTicket() => _hub.Submit(new CustomerForm());

        [Fact]
        public async Task ApproveIsRejectedWhileAnotherReviewerHoldsTheTicket()
        {
            var ticket = await NewTicket();
            await using (await HoldLock(ticket.TicketId))
            {
                var response = await _hub.Approve(_hub.Two, ticket.TicketId);

                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Contains("Another reviewer is working on this ticket", await response.Content.ReadAsStringAsync());
            }
            Assert.Equal("Review", (await _hub.Read(ticket.TicketId)).TicketStatus);
        }

        [Fact]
        public async Task RejectIsRejectedWhileAnotherReviewerHoldsTheTicket()
        {
            var ticket = await NewTicket();
            await using (await HoldLock(ticket.TicketId))
            {
                var response = await _hub.Reject(_hub.Two, ticket.TicketId);

                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                Assert.Contains("Another reviewer is working on this ticket", await response.Content.ReadAsStringAsync());
            }
            var after = await _hub.Read(ticket.TicketId);
            Assert.Equal("Review", after.TicketStatus);
            Assert.Null(after.Decision);
        }

        [Fact]
        public async Task ClearingAFlagIsRejectedWhileAnotherReviewerHoldsTheTicket()
        {
            var form = new CustomerForm { FirstName = "James", MiddleName = "Robert", LastName = "Anderson" }; // an OFAC match raises a flag
            var ticket = await _hub.Submit(form);
            var flag = Assert.Single(ticket.Flags);
            await using (await HoldLock(ticket.TicketId))
            {
                var response = await _hub.Two.PostAsJsonAsync($"/api/admins/tickets/{ticket.TicketId}/flags/{flag.FlagId}/clear",
                    new { reason = "Reviewed the customer's documents and cleared the flag." });

                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }
            Assert.All((await _hub.Read(ticket.TicketId)).Flags, f => Assert.False(f.Cleared));
        }

        [Fact]
        public async Task TheTicketWorksAgainOnceTheOtherReviewerIsDone()
        {
            var ticket = await NewTicket();
            await using (await HoldLock(ticket.TicketId))
            {
                Assert.Equal(HttpStatusCode.Conflict, (await _hub.Approve(_hub.Two, ticket.TicketId)).StatusCode);
            }

            var response = await _hub.Approve(_hub.Two, ticket.TicketId);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Approved", (await _hub.Read(ticket.TicketId)).TicketStatus);
        }

        [Fact]
        public async Task LockingOneTicketDoesNotBlockAnother()
        {
            var locked = await NewTicket();
            var other = await NewTicket();
            await using (await HoldLock(locked.TicketId))
            {
                var response = await _hub.Approve(_hub.Two, other.TicketId);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Fact]
        public async Task OnlyOneOfTwoSimultaneousRejectionsGoesThrough()
        {
            var ticket = await NewTicket();

            var responses = await Task.WhenAll(_hub.Reject(_hub.One, ticket.TicketId, "First reviewer's reason."), _hub.Reject(_hub.Two, ticket.TicketId, "Second reviewer's reason."));

            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).OrderBy(s => (int)s));
            var after = await _hub.Read(ticket.TicketId);
            Assert.Equal("Rejected", after.TicketStatus);
            Assert.Equal(1, _hub.CountRows("Decisions", ticket.TicketId));
        }

        [Fact]
        public async Task OnlyOneOfTwoSimultaneousApprovalsGoesThrough()
        {
            var ticket = await NewTicket();

            var responses = await Task.WhenAll(_hub.Approve(_hub.One, ticket.TicketId), _hub.Approve(_hub.Two, ticket.TicketId));

            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).OrderBy(s => (int)s));
            Assert.Equal("Approved", (await _hub.Read(ticket.TicketId)).TicketStatus);
            Assert.Equal(1, _hub.CountRows("Decisions", ticket.TicketId));
        }

        [Fact]
        public async Task ApprovingAndRejectingAtTheSameTimeLeavesExactlyOneDecision()
        {
            var ticket = await NewTicket();

            var responses = await Task.WhenAll(_hub.Approve(_hub.One, ticket.TicketId), _hub.Reject(_hub.Two, ticket.TicketId));

            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).OrderBy(s => (int)s));
            Assert.Equal(1, _hub.CountRows("Decisions", ticket.TicketId));
        }
    }
}
