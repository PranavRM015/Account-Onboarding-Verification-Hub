using System.Net;

namespace Integration.Tests;

public class ReviewWorkflowTests
{
    [Fact]
    public async Task Reject_RecordsTheDecisionWithTheReviewersNameAndReason()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;

        (await Stack.Reject(one, id, "Documents did not match.")).AssertOk("reject");

        var ticket = await Stack.ReadTicket(one, id);
        Assert.Equal("Rejected", ticket.Status);
        Assert.Equal(one.Username, ticket.DecidedBy);
        Assert.Equal("Documents did not match.", ticket.DecisionReason);
    }

    [Fact]
    public async Task ATicketThatHasADecision_CannotBeDecidedAgain()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        (await Stack.Approve(one, id)).AssertOk("approve");

        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Approve(one, id)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Reject(one, id)).Status);
        Assert.Equal("Approve", (await Stack.ReadTicket(one, id)).DecisionType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ADecisionNeedsAReason(string reason)
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;

        Assert.Equal(400, (await Stack.Approve(one, id, reason)).Code);
        Assert.Equal(400, (await Stack.Reject(one, id, reason)).Code);
        Assert.Equal(400, (await Stack.Approve(one, id, new string('x', 501))).Code);
        Assert.Equal("Review", (await Stack.ReadTicket(one, id)).Status);
    }

    [Fact]
    public async Task UnknownTicket_Is404_ForEveryAction()
    {
        var one = await Reviewers.One();

        Assert.Equal(404, (await Stack.Get($"{Stack.ReviewUrl}/api/admins/tickets/2000000000", one.Token)).Code);
        Assert.Equal(404, (await Stack.Approve(one, 2000000000)).Code);
        Assert.Equal(404, (await Stack.Reject(one, 2000000000)).Code);
    }

    [Fact]
    public async Task TheReviewQueue_ListsOnlyTicketsInReview_OldestDeadlineFirst()
    {
        var one = await Reviewers.One();
        var open = (await Stack.Onboard(one)).TicketId;
        var decided = (await Stack.Onboard(one)).TicketId;
        (await Stack.Reject(one, decided)).AssertOk("reject");

        var list = await Stack.Get($"{Stack.ReviewUrl}/api/admins/tickets", one.Token);

        list.AssertOk("list");
        var rows = list.Json.EnumerateArray().ToList();
        var ids = rows.Select(r => r.GetProperty("ticketId").GetInt32()).ToList();
        Assert.Contains(open, ids);
        Assert.DoesNotContain(decided, ids);
        var deadlines = rows.Select(r => r.GetProperty("verificationDeadline").GetDateTime()).ToList();
        Assert.Equal(deadlines.OrderBy(d => d), deadlines);
    }

    [Fact]
    public async Task TwoReviewersApprovingAtOnce_OnlyOneWins()
    {
        var one = await Reviewers.One();
        var two = await Reviewers.Two();
        var id = (await Stack.Onboard(one)).TicketId;

        var results = await Task.WhenAll(Stack.Approve(one, id, "Looks fine."), Stack.Approve(two, id, "Also fine."));

        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Conflict));
        Assert.Equal("Approved", (await Stack.ReadTicket(one, id)).Status);
    }

    [Fact]
    public async Task JointAccount_CarriesTheSecondHoldersName()
    {
        var one = await Reviewers.One();
        var first = await Stack.Onboard(one);

        var joint = await Stack.Onboard(one, f => { f.AccountType = "Joint"; f.JointCustomerId = first.CustomerId; });

        var ticket = await Stack.ReadTicket(one, joint.TicketId);
        Assert.Equal("Review", ticket.Status);
        Assert.Contains(first.Form.LastName, (await Stack.Get($"{Stack.ReviewUrl}/api/admins/tickets/{joint.TicketId}", one.Token)).Body);
    }

    [Fact]
    public async Task JointAccount_WithAnUnknownSecondCustomer_IsRefused()
    {
        var one = await Reviewers.One();

        await Stack.Onboard(one, f => { f.AccountType = "Joint"; f.JointCustomerId = 2000000000; }, expectStatus: 400);
    }

    [Fact]
    public async Task TheSameTaxId_CannotBeOnboardedTwice()
    {
        var one = await Reviewers.One();
        var first = await Stack.Onboard(one);

        var again = await Stack.Post($"{Stack.IntakeUrl}/api/admins/inputForm", first.Form.ToBodyWithNewUsername(), one.Token);

        Assert.Equal(HttpStatusCode.Conflict, again.Status);
        Assert.Contains("tax ID", again.Body);
    }

    [Fact]
    public async Task AWeakPassword_IsRefused_AndLeavesNothingBehind()
    {
        var one = await Reviewers.One();
        var form = new CustomerForm { Password = "weak" };

        var first = await Stack.Post($"{Stack.IntakeUrl}/api/admins/inputForm", form.ToBody(), one.Token);
        Assert.Equal(400, first.Code);

        // The profile was rolled back, so the same tax id can be entered again with a proper password.
        form.Password = Stack.Password;
        (await Stack.Post($"{Stack.IntakeUrl}/api/admins/inputForm", form.ToBody(), one.Token)).AssertOk("retry");
    }

    [Fact]
    public async Task ATakenUsername_IsAConflict()
    {
        var one = await Reviewers.One();
        var first = await Stack.Onboard(one);

        var clash = await Stack.Post($"{Stack.IntakeUrl}/api/admins/inputForm", new CustomerForm { Username = first.Form.Username }.ToBody(), one.Token);

        Assert.Equal(HttpStatusCode.Conflict, clash.Status);
    }

    [Fact]
    public async Task TicketHistory_ShowsEachStep_InTheAuditTrail()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one, CustomerForm.Raising("ClosedJurisdiction"))).TicketId;
        await Stack.ClearAllFlags(one, id);
        (await Stack.Approve(one, id)).AssertOk("approve");

        var messages = await Stack.Sql("SELECT ATMessage FROM AuditTrails WHERE TicketId = @id ORDER BY RecId", async c =>
        {
            var list = new List<string>();
            await using var reader = await c.ExecuteReaderAsync();
            while (await reader.ReadAsync()) list.Add(reader.GetString(0));
            return list;
        }, ("@id", id));

        Assert.Equal(new[] { "Ticket created", "Validation pipeline completed", "ClosedJurisdiction flag cleared", "Ticket approved" }, messages);
    }
}
