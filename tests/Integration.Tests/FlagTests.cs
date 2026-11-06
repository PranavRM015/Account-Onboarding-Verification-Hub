using System.Net;

namespace Integration.Tests;

// Customer -> Intake -> Risk -> Review, for every flag Risk can raise and every combination of them.
public class FlagTests
{
    private static readonly string[] AllFlags = { "ClosedJurisdiction", "OfacMatch", "ProhibitedIndustry", "VerificationFailed" }; // alphabetical, like Ticket.FlagTypes

    public static IEnumerable<object[]> EveryCombination() => Enumerable.Range(0, 16).Select(mask => new object[] { mask });
    private static string[] Subset(int mask) => AllFlags.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
    private static string Describe(string[] flags) => flags.Length == 0 ? "no flags" : string.Join("+", flags);

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public async Task TicketCarriesExactlyTheFlagsAndRatingRiskProduced(int mask)
    {
        var flags = Subset(mask);
        var admin = await Reviewers.One();

        var onboarded = await Stack.Onboard(admin, CustomerForm.Raising(flags));
        var ticket = await Stack.ReadTicket(admin, onboarded.TicketId);

        Assert.True(onboarded.TicketId > 0, Describe(flags));
        Assert.Equal("Review", ticket.Status);
        Assert.Equal(flags, ticket.FlagTypes);
        Assert.Equal(CustomerForm.RatingFor(flags), ticket.RiskRating);
        Assert.Equal(flags.Length == 0, ticket.Verified);
        Assert.All(ticket.Flags, f => Assert.False(f.Cleared));
        Assert.Equal(admin.Username, ticket.AssignedTo);
        Assert.Equal(ticket.CreatedAt.AddDays(60), ticket.Deadline);
        Assert.Equal(onboarded.Form.LastName, ticket.Customer.GetProperty("lastName").GetString()); // Review stored Intake's snapshot
    }

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public async Task VendorFindingsMatchTheFlags(int mask)
    {
        var flags = Subset(mask);
        var admin = await Reviewers.One();

        var ticket = await Stack.ReadTicket(admin, (await Stack.Onboard(admin, CustomerForm.Raising(flags))).TicketId);

        bool failed = flags.Contains("VerificationFailed");
        Assert.Equal(failed, ticket.Vendor.GetProperty("score").GetInt32() < 50);
        Assert.Equal(failed, ticket.Vendor.GetProperty("riskCodes").GetString()!.Length > 0);
        Assert.Equal(failed ? "NoMatch" : "Match", ticket.Vendor.GetProperty("taxIdMatch").GetString());
    }

    [Fact]
    public async Task CorrectionalAddress_AlsoRaisesVerificationFailed()
    {
        var admin = await Reviewers.One();

        var ticket = await Stack.ReadTicket(admin, (await Stack.Onboard(admin, f => f.Address = "500 County Jail Road, Austin TX")).TicketId);

        Assert.Equal(new[] { "VerificationFailed" }, ticket.FlagTypes);
        Assert.Equal("CorrectionalAddress", ticket.Vendor.GetProperty("riskCodes").GetString());
        Assert.Equal("Medium", ticket.RiskRating);
    }

    [Theory]
    [InlineData("MX", "MX", false)]
    [InlineData("US", "MX", false)]
    [InlineData("US", "IR", true)]
    [InlineData("CU", "US", true)]
    [InlineData("CA", "CA", true)]
    public async Task ClosedJurisdiction_DependsOnResidenceAndCitizenship(string residence, string citizenship, bool flagged)
    {
        var admin = await Reviewers.One();

        var ticket = await Stack.ReadTicket(admin, (await Stack.Onboard(admin, f => { f.Residence = residence; f.Citizenship = citizenship; })).TicketId);

        Assert.Equal(flagged, ticket.FlagTypes.Contains("ClosedJurisdiction"));
    }

    [Theory]
    [InlineData("Marijuana grower", "School", true)]
    [InlineData("Teacher", "Hemp & CBD Wholesale", true)]
    [InlineData("Teacher", "School", false)]
    public async Task ProhibitedIndustry_ComesFromOccupationOrEmployer(string occupation, string employer, bool flagged)
    {
        var admin = await Reviewers.One();

        var ticket = await Stack.ReadTicket(admin, (await Stack.Onboard(admin, f => { f.Occupation = occupation; f.Employer = employer; })).TicketId);

        Assert.Equal(flagged, ticket.FlagTypes.Contains("ProhibitedIndustry"));
    }

    // ---- clearing flags and approving ---------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public async Task AllFlagsMustBeClearedBeforeApproval_ThenTheRatingDecidesHowManyApprovalsAreNeeded(int mask)
    {
        var flags = Subset(mask);
        var one = await Reviewers.One();
        var two = await Reviewers.Two();
        var id = (await Stack.Onboard(one, CustomerForm.Raising(flags))).TicketId;

        if (flags.Length > 0)
        {
            Assert.Equal(HttpStatusCode.Conflict, (await Stack.Approve(one, id)).Status); // open flags block approval
            var firstFlag = (await Stack.ReadTicket(one, id)).Flags[0].FlagId;
            Assert.Equal(400, (await Stack.ClearFlag(one, id, firstFlag, reason: "  ")).Code); // a reason is required
            await Stack.ClearAllFlags(one, id);
            Assert.All((await Stack.ReadTicket(one, id)).Flags, f => { Assert.True(f.Cleared); Assert.Equal(one.Username, f.ClearedBy); Assert.Equal("Checked in person.", f.ClearedReason); });
        }

        if (CustomerForm.RatingFor(flags) == "High")
        {
            var first = await Stack.Approve(one, id);
            first.AssertOk("first approval");
            Assert.Contains("First approval recorded", first.Body);
            Assert.Equal("Review", (await Stack.ReadTicket(one, id)).Status);
            Assert.Equal(one.Username, (await Stack.ReadTicket(one, id)).FirstApprovedBy);
            Assert.Equal(HttpStatusCode.Conflict, (await Stack.Approve(one, id)).Status); // the same reviewer cannot approve twice
            (await Stack.Approve(two, id)).AssertOk("second approval");
            Assert.Equal(two.Username, (await Stack.ReadTicket(one, id)).DecidedBy);
        }
        else
        {
            (await Stack.Approve(one, id)).AssertOk("approval");
            Assert.Equal(one.Username, (await Stack.ReadTicket(one, id)).DecidedBy);
        }

        var done = await Stack.ReadTicket(one, id);
        Assert.Equal("Approved", done.Status);
        Assert.Equal("Approve", done.DecisionType);
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Reject(one, id)).Status);
    }

    [Theory]
    [InlineData("VerificationFailed")]
    [InlineData("OfacMatch")]
    [InlineData("ClosedJurisdiction")]
    [InlineData("ProhibitedIndustry")]
    public async Task ATicketWithOpenFlags_CanStillBeRejected(string flag)
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one, CustomerForm.Raising(flag))).TicketId;

        (await Stack.Reject(one, id, "Customer is on a watch list.")).AssertOk("reject");

        var ticket = await Stack.ReadTicket(one, id);
        Assert.Equal("Rejected", ticket.Status);
        Assert.Equal("Reject", ticket.DecisionType);
        Assert.Equal("Customer is on a watch list.", ticket.DecisionReason);
        Assert.Equal(new[] { flag }, ticket.FlagTypes);
        Assert.False(ticket.Flags.Single().Cleared);
    }

    [Fact]
    public async Task AFlag_CannotBeClearedTwice_OrThroughAnotherTicket_OrAfterTheDecision()
    {
        var one = await Reviewers.One();
        var flagged = (await Stack.Onboard(one, CustomerForm.Raising("ClosedJurisdiction"))).TicketId;
        var other = (await Stack.Onboard(one, CustomerForm.Raising("ClosedJurisdiction"))).TicketId;
        var flagId = (await Stack.ReadTicket(one, flagged)).Flags.Single().FlagId;

        (await Stack.ClearFlag(one, flagged, flagId)).AssertOk("first clear");
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.ClearFlag(one, flagged, flagId)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await Stack.ClearFlag(one, other, flagId)).Status);

        (await Stack.Approve(one, flagged)).AssertOk("approve");
        var otherFlag = (await Stack.ReadTicket(one, other)).Flags.Single().FlagId;
        (await Stack.Reject(one, other)).AssertOk("reject");
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.ClearFlag(one, other, otherFlag)).Status);
    }

    [Fact]
    public async Task ClearingOneOfSeveralFlags_IsNotEnoughToApprove()
    {
        var one = await Reviewers.One();
        var id = (await Stack.Onboard(one, CustomerForm.Raising("ClosedJurisdiction", "VerificationFailed"))).TicketId;
        var flags = (await Stack.ReadTicket(one, id)).Flags;

        (await Stack.ClearFlag(one, id, flags[0].FlagId)).AssertOk("clear one");

        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Approve(one, id)).Status);
        (await Stack.ClearFlag(one, id, flags[1].FlagId)).AssertOk("clear two");
        (await Stack.Approve(one, id)).AssertOk("approve");
    }
}
