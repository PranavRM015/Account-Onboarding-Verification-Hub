using System.Net;
using TicketDtos;

namespace backend.Tests
{
    public record Scenario(Action<CustomerForm> Apply, string[] Flags, string Rating);

    public class ReviewScenarioTests : IClassFixture<HubFixture>
    {
        private static readonly string[] Keywords = { "cannabis", "marijuana", "dispensary", "hemp", "thc", "cbd" };

        private static readonly Dictionary<string, Scenario> Scenarios = BuildScenarios();

        private readonly HubFixture _hub;

        public ReviewScenarioTests(HubFixture hub)
        {
            _hub = hub;
        }

        private static Dictionary<string, Scenario> BuildScenarios()
        {
            var scenarios = new Dictionary<string, Scenario>
            {
                ["Clean customer"] = new(f => { }, Array.Empty<string>(), "Low"),
                ["OFAC match"] = new(f =>
                {
                    f.FirstName = "James";
                    f.MiddleName = "Robert";
                    f.LastName = "Anderson";
                }, new[] { "OfacMatch" }, "High"),
                ["Citizen of IN living in US"] = new(f =>
                {
                    f.CountryOfCitizenship = "IN";
                    f.CountryOfResidence = "US";
                }, new[] { "ClosedJurisdiction" }, "Medium"),
                ["Citizen of IN living in MX"] = new(f =>
                {
                    f.CountryOfCitizenship = "IN";
                    f.CountryOfResidence = "MX";
                }, new[] { "ClosedJurisdiction" }, "Medium"),
                ["Citizen of IN living in GB"] = new(f =>
                {
                    f.CountryOfCitizenship = "IN";
                    f.CountryOfResidence = "GB";
                }, new[] { "ClosedJurisdiction" }, "Medium"),
                ["Citizen of US living in GB"] = new(f =>
                {
                    f.CountryOfCitizenship = "US";
                    f.CountryOfResidence = "GB";
                }, new[] { "ClosedJurisdiction" }, "Medium"),
            };
            foreach (var keyword in Keywords)
            {
                scenarios[$"Occupation contains {keyword}"] = new(
                    f => f.Occupation = $"Senior {keyword} specialist",
                    new[] { "ProhibitedIndustry" }, "High");
                scenarios[$"Employer contains {keyword}"] = new(
                    f => f.Employer = $"Northern {keyword} Holdings",
                    new[] { "ProhibitedIndustry" }, "High");
            }
            return scenarios;
        }

        public static IEnumerable<object[]> Cases()
        {
            foreach (var name in Scenarios.Keys)
            {
                yield return new object[] { name, true };
                yield return new object[] { name, false };
            }
        }

        private async Task<TicketDetailsDto> SubmitScenario(string name)
        {
            var scenario = Scenarios[name];
            var form = new CustomerForm();
            scenario.Apply(form);
            var ticket = await _hub.Submit(form);
            Assert.Equal("Review", ticket.TicketStatus);
            Assert.Equal(scenario.Rating, ticket.RiskRating);
            Assert.Equal(scenario.Flags.OrderBy(x => x), ticket.Flags.Select(f => f.ViolationType).OrderBy(x => x));
            Assert.All(ticket.Flags, f => Assert.False(f.Cleared));
            Assert.Equal(scenario.Flags.Length == 0, ticket.Verified);
            return ticket;
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public async Task ScenarioCanBeAcceptedOrRejected(string name, bool accept)
        {
            var ticket = await SubmitScenario(name);

            if (!accept)
            {
                var reject = await _hub.Reject(_hub.One, ticket.TicketId);
                Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
                var rejected = await _hub.Read(ticket.TicketId);
                Assert.Equal("Rejected", rejected.TicketStatus);
                Assert.Equal("Reject", rejected.Decision!.DecisionType);
                Assert.Equal(_hub.ReviewerOne, rejected.Decision.DecidedBy);
                var approveAfter = await _hub.Approve(_hub.One, ticket.TicketId);
                Assert.Equal(HttpStatusCode.Conflict, approveAfter.StatusCode);
                return;
            }

            await _hub.ClearAllFlags(ticket);
            var first = await _hub.Approve(_hub.One, ticket.TicketId);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            if (ticket.RiskRating == "High")
            {
                var afterFirst = await _hub.Read(ticket.TicketId);
                Assert.Equal("Review", afterFirst.TicketStatus);
                Assert.Equal(_hub.ReviewerOne, afterFirst.FirstApprovedBy);
                var sameReviewer = await _hub.Approve(_hub.One, ticket.TicketId);
                Assert.Equal(HttpStatusCode.Conflict, sameReviewer.StatusCode);
                var second = await _hub.Approve(_hub.Two, ticket.TicketId);
                Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            }
            var approved = await _hub.Read(ticket.TicketId);
            Assert.Equal("Approved", approved.TicketStatus);
            Assert.Equal("Approve", approved.Decision!.DecisionType);
            Assert.All(approved.Flags, f => Assert.True(f.Cleared));
        }

        [Fact]
        public async Task TicketWithAnUnclearedFlagCannotBeApproved()
        {
            var ticket = await SubmitScenario("OFAC match");
            var response = await _hub.Approve(_hub.One, ticket.TicketId);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("Review", (await _hub.Read(ticket.TicketId)).TicketStatus);
        }

        [Fact]
        public async Task ApprovalWithoutAReasonIsRejected()
        {
            var ticket = await SubmitScenario("Clean customer");
            var response = await _hub.Approve(_hub.One, ticket.TicketId, "   ");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RejectionWithoutAReasonIsRejected()
        {
            var ticket = await SubmitScenario("Clean customer");
            var response = await _hub.Reject(_hub.One, ticket.TicketId, "");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
