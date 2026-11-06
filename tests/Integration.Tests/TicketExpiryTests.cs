using System.Net;

namespace Integration.Tests;

// services/review/database/Procedures.sql: pr_expireReviewTickets and the three procedures it calls.
// Needs REVIEW_DB_CONNECTION, and the procedures must have been applied to the Review database.
public class TicketExpiryTests
{
    private static readonly string[] AllFlags = { "ClosedJurisdiction", "OfacMatch", "ProhibitedIndustry", "VerificationFailed" };

    private static async Task<List<(string Prev, string Curr, string Message, string Reason)>> Audit(int ticketId) =>
        await Stack.Sql("SELECT PrevStatus, CurrStatus, ATMessage, Reason FROM AuditTrails WHERE TicketId = @id ORDER BY RecId", async c =>
        {
            var rows = new List<(string, string, string, string)>();
            await using var reader = await c.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            return rows;
        }, ("@id", ticketId));

    private static async Task<int> Decisions(int ticketId) => await Stack.Scalar<int>("SELECT COUNT(*) FROM Decisions WHERE TicketId = @id", ("@id", ticketId));

    [Fact]
    public async Task TicketPastItsDeadline_IsRejectedBySystem_WithADecisionAndAnAuditRow()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        await Stack.Age(id, 61);

        await Stack.RunExpiry();

        var ticket = await Stack.ReadTicket(one, id);
        Assert.Equal("Rejected", ticket.Status);
        Assert.Equal("Reject", ticket.DecisionType);
        Assert.Equal("system", ticket.DecidedBy);
        Assert.Equal("Not resolved within 60 days", ticket.DecisionReason);
        Assert.Contains(("Review", "Rejected", "Rejected automatically", "Not resolved within 60 days"), await Audit(id));
        Assert.Equal(1, await Decisions(id));
    }

    [Theory]
    [InlineData("VerificationFailed")]
    [InlineData("OfacMatch")]
    [InlineData("ClosedJurisdiction")]
    [InlineData("ProhibitedIndustry")]
    public async Task OverdueTicketsAreRejected_WhateverFlagTheyCarry_AndTheFlagsStayOnRecord(string flag)
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one, CustomerForm.Raising(flag))).TicketId;
        await Stack.Age(id, 61);

        await Stack.RunExpiry();

        var ticket = await Stack.ReadTicket(one, id);
        Assert.Equal("Rejected", ticket.Status);
        Assert.Equal("system", ticket.DecidedBy);
        Assert.Equal(new[] { flag }, ticket.FlagTypes);
        Assert.False(ticket.Flags.Single().Cleared);
    }

    [Fact]
    public async Task OverdueTicketWithEveryFlag_IsRejected()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one, CustomerForm.Raising(AllFlags))).TicketId;
        await Stack.Age(id, 90);

        await Stack.RunExpiry();

        Assert.Equal("Rejected", (await Stack.ReadTicket(one, id)).Status);
        Assert.Equal(AllFlags, (await Stack.ReadTicket(one, id)).FlagTypes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(59)]
    public async Task TicketInsideItsDeadline_IsLeftInReview(int daysOld)
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        if (daysOld > 0) await Stack.Age(id, daysOld);

        await Stack.RunExpiry();

        var ticket = await Stack.ReadTicket(one, id);
        Assert.Equal("Review", ticket.Status);
        Assert.Null(ticket.Decision);
        Assert.Equal(0, await Decisions(id));
    }

    [Fact]
    public async Task TicketThatAlreadyHasADecision_IsNotTouched()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        (await Stack.Reject(one, id, "Not eligible.")).AssertOk("reject");
        await Stack.Age(id, 61);

        await Stack.RunExpiry();

        var ticket = await Stack.ReadTicket(one, id);
        Assert.Equal(one.Username, ticket.DecidedBy);
        Assert.Equal("Not eligible.", ticket.DecisionReason);
        Assert.Equal(1, await Decisions(id));
    }

    [Fact]
    public async Task ApprovedTicket_IsNotRejectedLater()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        (await Stack.Approve(one, id)).AssertOk("approve");
        await Stack.Age(id, 61);

        await Stack.RunExpiry();

        Assert.Equal("Approved", (await Stack.ReadTicket(one, id)).Status);
    }

    [Fact]
    public async Task RunningItTwice_ChangesNothingTheSecondTime()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        await Stack.Age(id, 61);

        await Stack.RunExpiry();
        var auditAfterFirst = (await Audit(id)).Count;
        await Stack.RunExpiry();

        Assert.Equal(auditAfterFirst, (await Audit(id)).Count);
        Assert.Equal(1, await Decisions(id));
    }

    [Fact]
    public async Task ManyOverdueTickets_AreAllRejectedInOneRun_AndOnlyTheOverdueOnes()
    {
        var one = await Reviewers.One();
        var overdue = new List<int>();
        for (int i = 0; i < 5; i++) { var id = (await Stack.Onboard(one)).TicketId; await Stack.Age(id, 61 + i); overdue.Add(id); }
        var fresh = (await Stack.Onboard(one)).TicketId;

        await Stack.RunExpiry();

        foreach (var id in overdue) Assert.Equal("Rejected", (await Stack.ReadTicket(one, id)).Status);
        Assert.Equal("Review", (await Stack.ReadTicket(one, fresh)).Status);
    }

    [Fact]
    public async Task AnExpiredTicket_CanNoLongerBeApprovedOrHaveFlagsCleared()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one, CustomerForm.Raising("ClosedJurisdiction"))).TicketId;
        var flag = (await Stack.ReadTicket(one, id)).Flags.Single().FlagId;
        await Stack.Age(id, 61);
        await Stack.RunExpiry();

        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Approve(one, id)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.ClearFlag(one, id, flag)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Reject(one, id)).Status);
    }

    [Fact]
    public async Task AnOverdueTicket_CannotBeDecidedByAReviewer_EvenBeforeTheJobHasRun()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        await Stack.Age(id, 61);

        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Approve(one, id)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Reject(one, id)).Status);
    }

    [Fact]
    public async Task ExpiredTickets_LeaveTheReviewQueue()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        await Stack.Age(id, 61);

        await Stack.RunExpiry();

        var list = await Stack.Get($"{Stack.ReviewUrl}/api/admins/tickets", one.Token);
        Assert.DoesNotContain(list.Json.EnumerateArray(), r => r.GetProperty("ticketId").GetInt32() == id);
    }

    [Fact]
    public async Task EachSubProcedure_DoesItsOwnJob()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        var customerId = await Stack.Scalar<int>("SELECT CustomerId FROM Tickets WHERE TicketId = @id", ("@id", id));

        await Stack.Exec("EXEC dbo.pr_ticketExpireReject @id", ("@id", id));
        Assert.Equal("Rejected", (await Stack.ReadTicket(one, id)).Status);
        Assert.Equal(0, await Decisions(id));

        await Stack.Exec("EXEC dbo.pr_decisionExpireReject @id, 'Reject', N'system', N'because'", ("@id", id));
        Assert.Equal(1, await Decisions(id));

        var before = (await Audit(id)).Count;
        await Stack.Exec("EXEC dbo.pr_auditTrailExpireReject @id, @c, 'Review', 'Rejected', N'm', N'r'", ("@id", id), ("@c", customerId));
        Assert.Equal(before + 1, (await Audit(id)).Count);
    }

    [Fact]
    public async Task ReviewTicketExpireReject_OnlyTouchesTicketsInReview()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one)).TicketId;
        (await Stack.Approve(one, id)).AssertOk("approve");

        await Stack.Exec("EXEC dbo.pr_ticketExpireReject @id", ("@id", id));

        Assert.Equal("Approved", (await Stack.ReadTicket(one, id)).Status);
    }
}
