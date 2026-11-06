using AuditTrails;
using Data;
using Decisions;
using Flags;
using Microsoft.EntityFrameworkCore;
using Tickets;
using VendorResponses;

namespace Review.Tests;

public class TicketTests
{
    [Fact]
    public void NewTicket_StartsSubmitted_WithADeadline60DaysAfterCreation()
    {
        var ticket = new Ticket(5, "Individual", "alice");

        Assert.Equal("Submitted", ticket.TicketStatus);
        Assert.Null(ticket.RiskRating);
        Assert.Equal(ticket.CreatedAt.AddDays(60), ticket.VerificationDeadline);
    }

    private static Ticket Valid(Action<Ticket>? change = null)
    {
        var ticket = new Ticket(5, "Individual", "alice") { RiskRating = "Low" };
        change?.Invoke(ticket);
        return ticket;
    }

    private static bool IsValid(Ticket ticket) => new TicketValidator().Validate(ticket).IsValid;

    [Fact] public void ValidTicket_Passes() => Assert.True(IsValid(Valid()));
    [Theory]
    [InlineData("Submitted")] [InlineData("Review")] [InlineData("Approved")] [InlineData("Rejected")]
    public void KnownStatuses_Pass(string status) => Assert.True(IsValid(Valid(t => t.TicketStatus = status)));
    [Fact] public void UnknownStatus_Fails() => Assert.False(IsValid(Valid(t => t.TicketStatus = "Pending")));
    [Theory]
    [InlineData("Low")] [InlineData("Medium")] [InlineData("High")]
    public void KnownRatings_Pass(string rating) => Assert.True(IsValid(Valid(t => t.RiskRating = rating)));
    [Fact] public void UnknownRating_Fails() => Assert.False(IsValid(Valid(t => t.RiskRating = "Extreme")));
    [Fact] public void UnknownAccountType_Fails() => Assert.False(IsValid(Valid(t => t.AccountType = "Business")));
    [Fact] public void DeadlineNot60Days_Fails() => Assert.False(IsValid(Valid(t => t.VerificationDeadline = t.CreatedAt.AddDays(59))));
    [Fact] public void AssignedTo_IsRequired() => Assert.False(IsValid(Valid(t => t.AssignedTo = "")));
    [Fact] public void AssignedTo_IsLimitedTo100() => Assert.False(IsValid(Valid(t => t.AssignedTo = new string('a', 101))));
    [Fact] public void CustomerId_MustBePositive() => Assert.False(IsValid(Valid(t => t.CustomerId = 0)));
    [Fact] public void Joint_NeedsASecondCustomer() => Assert.False(IsValid(Valid(t => t.AccountType = "Joint")));
    [Fact] public void Joint_SecondCustomerMustDiffer() => Assert.False(IsValid(Valid(t => { t.AccountType = "Joint"; t.JointCustomerId = t.CustomerId; })));
    [Fact] public void Joint_WithADifferentCustomer_Passes() => Assert.True(IsValid(Valid(t => { t.AccountType = "Joint"; t.JointCustomerId = 9; })));
    [Fact] public void Individual_CannotHaveASecondCustomer() => Assert.False(IsValid(Valid(t => t.JointCustomerId = 9)));
}

public class FlagTests
{
    private static bool IsValid(Flag flag) => new FlagValidator().Validate(flag).IsValid;

    [Fact]
    public void ThereAreExactlyFourViolationTypes() =>
        Assert.Equal(new[] { "VerificationFailed", "OfacMatch", "ProhibitedIndustry", "ClosedJurisdiction" }.OrderBy(x => x), FlagValues.ViolationTypes.OrderBy(x => x));

    [Theory]
    [InlineData("VerificationFailed")]
    [InlineData("OfacMatch")]
    [InlineData("ProhibitedIndustry")]
    [InlineData("ClosedJurisdiction")]
    public void EachViolationType_IsAValidNewFlag(string type)
    {
        var flag = new Flag(3, type, raisedBy: 1);

        Assert.True(IsValid(flag));
        Assert.Null(flag.ClearedAt);
        Assert.Equal(1, flag.RaisedBy);
    }

    [Fact] public void FlagRaisedBySystem_HasNoRaiser() => Assert.Null(new Flag(3, "OfacMatch", null).RaisedBy);
    [Fact] public void UnknownViolationType_Fails() => Assert.False(IsValid(new Flag(3, "Fraud", 1)));
    [Fact] public void TicketId_MustBePositive() => Assert.False(IsValid(new Flag(0, "OfacMatch", 1)));

    [Theory]
    [InlineData("VerificationFailed")]
    [InlineData("OfacMatch")]
    [InlineData("ProhibitedIndustry")]
    [InlineData("ClosedJurisdiction")]
    public void ClearedFlag_NeedsWhoAndWhy(string type)
    {
        var cleared = new Flag(3, type, 1) { ClearedAt = DateTime.UtcNow, ClearedBy = 2, ClearedByName = "bob", ClearedReason = "Verified in person" };
        Assert.True(IsValid(cleared));

        Assert.False(IsValid(new Flag(3, type, 1) { ClearedAt = DateTime.UtcNow, ClearedBy = 2, ClearedReason = "" }));
        Assert.False(IsValid(new Flag(3, type, 1) { ClearedAt = DateTime.UtcNow, ClearedBy = null, ClearedReason = "Verified" }));
    }

    [Fact] public void ClearedReason_IsLimitedTo500() =>
        Assert.False(IsValid(new Flag(3, "OfacMatch", 1) { ClearedAt = DateTime.UtcNow, ClearedBy = 2, ClearedReason = new string('x', 501) }));
}

public class DecisionTests
{
    private static bool IsValid(Decision d) => new DecisionValidator().Validate(d).IsValid;

    [Theory] [InlineData("Approve")] [InlineData("Reject")]
    public void ApproveAndReject_AreValid(string type) => Assert.True(IsValid(new Decision(1, type, "alice", "Because")));
    [Fact] public void OtherDecisionTypes_Fail() => Assert.False(IsValid(new Decision(1, "Defer", "alice", "Because")));
    [Fact] public void Reason_IsRequired() => Assert.False(IsValid(new Decision(1, "Approve", "alice", "")));
    [Fact] public void Reason_IsLimitedTo1000() => Assert.False(IsValid(new Decision(1, "Approve", "alice", new string('x', 1001))));
    [Fact] public void DecidedBy_IsRequiredAndLimitedTo100() { Assert.False(IsValid(new Decision(1, "Approve", "", "r"))); Assert.False(IsValid(new Decision(1, "Approve", new string('a', 101), "r"))); }
    [Fact] public void TicketId_MustBePositive() => Assert.False(IsValid(new Decision(0, "Approve", "alice", "r")));
}

public class AuditTrailTests
{
    private static bool IsValid(AuditTrail a) => new AuditTrailValidator().Validate(a).IsValid;

    [Fact] public void ValidRow_Passes() => Assert.True(IsValid(new AuditTrail(1, 2, "Review", "Rejected", "Rejected automatically", "Not resolved within 60 days")));
    [Fact] public void UnknownStatus_Fails() { Assert.False(IsValid(new AuditTrail(1, 2, "Open", "Rejected", "m", "r"))); Assert.False(IsValid(new AuditTrail(1, 2, "Review", "Closed", "m", "r"))); }
    [Fact] public void MessageAndReason_AreRequired() { Assert.False(IsValid(new AuditTrail(1, 2, "Review", "Approved", "", "r"))); Assert.False(IsValid(new AuditTrail(1, 2, "Review", "Approved", "m", ""))); }
    [Fact] public void MessageIsLimitedTo250_ReasonTo500() { Assert.False(IsValid(new AuditTrail(1, 2, "Review", "Approved", new string('m', 251), "r"))); Assert.False(IsValid(new AuditTrail(1, 2, "Review", "Approved", "m", new string('r', 501)))); }
    [Fact] public void Ids_MustBePositive() { Assert.False(IsValid(new AuditTrail(0, 2, "Review", "Approved", "m", "r"))); Assert.False(IsValid(new AuditTrail(1, 0, "Review", "Approved", "m", "r"))); }
}

public class VendorResponseTests
{
    private static bool IsValid(VendorResponse v) => new VendorResponseValidator().Validate(v).IsValid;

    [Fact] public void ValidResponse_Passes() => Assert.True(IsValid(new VendorResponse(1, "Match", "Match", "Partial", "NoMatch", 30, "DeceasedSSN")));
    [Fact] public void UnknownMatchValue_Fails() => Assert.False(IsValid(new VendorResponse(1, "Yes", "Match", "Match", "Match", 50, "")));
    [Theory] [InlineData(-1)] [InlineData(51)]
    public void ScoreOutsideRange_Fails(int score) => Assert.False(IsValid(new VendorResponse(1, "Match", "Match", "Match", "Match", score, "")));
    [Fact] public void RiskCodes_AreLimitedTo200() => Assert.False(IsValid(new VendorResponse(1, "Match", "Match", "Match", "Match", 50, new string('x', 201))));
}

// Decisions and audit rows may be added but never changed or removed.
public class ReviewDbContextImmutabilityTests
{
    private static ReviewDbContext NewContext() => new(new DbContextOptionsBuilder<ReviewDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<(ReviewDbContext db, Ticket ticket)> WithTicket()
    {
        var db = NewContext();
        var ticket = new Ticket(5, "Individual", "alice") { TicketStatus = "Review", RiskRating = "Low", CustomerJson = "{}", RequestHash = "x" };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return (db, ticket);
    }

    [Fact]
    public async Task DecisionsAndAuditRows_CanBeAdded()
    {
        var (db, ticket) = await WithTicket();
        db.Decisions.Add(new Decision(ticket.TicketId, "Reject", "system", "Not resolved within 60 days"));
        db.AuditTrails.Add(new AuditTrail(ticket.TicketId, 5, "Review", "Rejected", "Rejected automatically", "Not resolved within 60 days"));

        Assert.Equal(2, await db.SaveChangesAsync());
    }

    [Fact]
    public async Task ChangingADecision_IsRefused()
    {
        var (db, ticket) = await WithTicket();
        var decision = new Decision(ticket.TicketId, "Approve", "alice", "ok");
        db.Decisions.Add(decision);
        await db.SaveChangesAsync();

        decision.Reason = "changed my mind";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletingAnAuditRow_IsRefused()
    {
        var (db, ticket) = await WithTicket();
        var audit = new AuditTrail(ticket.TicketId, 5, "Review", "Approved", "m", "r");
        db.AuditTrails.Add(audit);
        await db.SaveChangesAsync();

        db.AuditTrails.Remove(audit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task OrdinaryTicketUpdates_AreStillAllowed()
    {
        var (db, ticket) = await WithTicket();
        ticket.TicketStatus = "Approved";

        Assert.Equal(1, await db.SaveChangesAsync());
    }
}
