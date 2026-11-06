using System.Net;

namespace Integration.Tests;

// The self-signup path: a customer registers, waits in the pending list, and an admin turns them into a ticket.
public class PendingCustomerTests
{
    private static readonly string[] AllFlags = { "ClosedJurisdiction", "OfacMatch", "ProhibitedIndustry", "VerificationFailed" };
    public static IEnumerable<object[]> EveryCombination() => Enumerable.Range(0, 16).Select(mask => new object[] { mask });

    private static async Task<Reply> SelfSignup(CustomerForm form) => await Stack.Post($"{Stack.IntakeUrl}/api/users/signup", new
    {
        username = form.Username, password = form.Password, passwordRecoveryQuestion = Stack.Question, passwordRecoveryAnswer = Stack.Answer,
        firstName = form.FirstName, middleName = form.MiddleName.Length == 0 ? null : form.MiddleName, lastName = form.LastName,
        dateOfBirth = "1990-01-01", gender = "Male", taxIdType = "SSN", taxIdNumber = form.TaxIdNumber,
        residentialAddress = form.Address, countryOfResidence = form.Residence, countryOfCitizenship = form.Citizenship,
        occupation = form.Occupation, employer = form.Employer,
    });

    private static async Task<List<System.Text.Json.JsonElement>> Pending(AdminSession admin)
    {
        var reply = await Stack.Get($"{Stack.IntakeUrl}/api/admins/pending-customers", admin.Token);
        reply.AssertOk("pending list");
        return reply.Json.EnumerateArray().ToList();
    }

    // The signup answer does not name the new customer, so find them by name and then by their unique address
    // (several customers can share a name, e.g. the ones built to match the OFAC list).
    private static async Task<int> Waiting(AdminSession admin, CustomerForm form)
    {
        foreach (var candidate in (await Pending(admin)).Where(c => c.GetProperty("lastName").GetString() == form.LastName))
        {
            int id = candidate.GetProperty("customerId").GetInt32();
            var details = await Stack.Get($"{Stack.IntakeUrl}/api/admins/pending-customers/{id}", admin.Token);
            if (details.Status == HttpStatusCode.OK && details.Json.GetProperty("residentialAddress").GetString() == form.Address) return id;
        }
        throw new InvalidOperationException($"{form.LastName} is not in the pending list.");
    }

    [Fact]
    public async Task ASelfRegisteredCustomer_WaitsInThePendingList_UntilAnAdminSubmitsThem()
    {
        var admin = await Reviewers.One();
        var form = new CustomerForm();
        (await SelfSignup(form)).AssertOk("signup");

        var id = await Waiting(admin, form);
        var details = await Stack.Get($"{Stack.IntakeUrl}/api/admins/pending-customers/{id}", admin.Token);
        details.AssertOk("details");
        Assert.Equal("4321", details.Json.GetProperty("taxIdLastFourDigits").GetString());
        Assert.DoesNotContain(form.TaxIdNumber, details.Body); // only the last four digits are ever shown

        var submit = await Stack.Post($"{Stack.IntakeUrl}/api/admins/pending-customers/{id}/submit", new { typeOfAccount = "Individual" }, admin.Token);
        submit.AssertOk("submit");

        Assert.DoesNotContain(await Pending(admin), c => c.GetProperty("customerId").GetInt32() == id);
        Assert.Equal("Review", (await Stack.ReadTicket(admin, submit.Json.GetProperty("ticketId").GetInt32())).Status);
    }

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public async Task SubmittingAPendingCustomer_RaisesExactlyTheFlagsTheirProfileTriggers(int mask)
    {
        var flags = AllFlags.Where((_, i) => (mask & (1 << i)) != 0).ToArray();
        var admin = await Reviewers.One();
        var form = new CustomerForm();
        CustomerForm.Raising(flags)(form);
        (await SelfSignup(form)).AssertOk("signup");
        var id = await Waiting(admin, form);

        var submit = await Stack.Post($"{Stack.IntakeUrl}/api/admins/pending-customers/{id}/submit", new { typeOfAccount = "Individual" }, admin.Token);

        submit.AssertOk("submit");
        var ticket = await Stack.ReadTicket(admin, submit.Json.GetProperty("ticketId").GetInt32());
        Assert.Equal(flags, ticket.FlagTypes);
        Assert.Equal(CustomerForm.RatingFor(flags), ticket.RiskRating);
    }

    [Fact]
    public async Task ACustomerWhoHasBeenSubmitted_CannotBeSubmittedAgain()
    {
        var admin = await Reviewers.One();
        var form = new CustomerForm();
        (await SelfSignup(form)).AssertOk("signup");
        var id = await Waiting(admin, form);
        var url = $"{Stack.IntakeUrl}/api/admins/pending-customers/{id}/submit";
        (await Stack.Post(url, new { typeOfAccount = "Individual" }, admin.Token)).AssertOk("first submit");

        var again = await Stack.Post(url, new { typeOfAccount = "Individual" }, admin.Token);

        Assert.Equal(HttpStatusCode.Conflict, again.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Stack.Get($"{Stack.IntakeUrl}/api/admins/pending-customers/{id}", admin.Token)).Status);
    }

    [Fact]
    public async Task ACustomerEnteredThroughTheInputForm_NeverAppearsInThePendingList()
    {
        var admin = await Reviewers.One();
        var entered = await Stack.Onboard(admin);

        Assert.DoesNotContain(await Pending(admin), c => c.GetProperty("customerId").GetInt32() == entered.CustomerId);
    }

    [Fact]
    public async Task ASelfRegisteredCustomer_CanLogIn_BeforeAnyTicketExists()
    {
        var form = new CustomerForm();
        (await SelfSignup(form)).AssertOk("signup");

        var login = await Stack.Post($"{Stack.IdentityUrl}/api/users/login", new { username = form.Username, password = form.Password });

        login.AssertOk("customer login");
        var me = await Stack.Get($"{Stack.IdentityUrl}/api/users/me", login.Json.GetProperty("token").GetString());
        Assert.Equal(form.Username, me.Json.GetProperty("username").GetString());
    }

    [Fact]
    public async Task SelfSignup_RefusesABadProfile_ATakenUsername_AndADuplicateTaxId()
    {
        var first = new CustomerForm();
        (await SelfSignup(first)).AssertOk("signup");

        Assert.Equal(400, (await SelfSignup(new CustomerForm { Residence = "ZZ" })).Code);
        Assert.Equal(HttpStatusCode.Conflict, (await SelfSignup(new CustomerForm { Username = first.Username })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await SelfSignup(new CustomerForm { TaxIdNumber = first.TaxIdNumber })).Status);
        Assert.Equal(400, (await SelfSignup(new CustomerForm { Password = "weak" })).Code);
    }

    [Fact]
    public async Task APendingCustomer_CanBeSubmittedAsAJointAccount_WithAnExistingSecondCustomer()
    {
        var admin = await Reviewers.One();
        var partner = await Stack.Onboard(admin);
        var form = new CustomerForm();
        (await SelfSignup(form)).AssertOk("signup");
        var id = await Waiting(admin, form);

        var bad = await Stack.Post($"{Stack.IntakeUrl}/api/admins/pending-customers/{id}/submit", new { typeOfAccount = "Joint", jointCustomerId = 2000000000 }, admin.Token);
        var good = await Stack.Post($"{Stack.IntakeUrl}/api/admins/pending-customers/{id}/submit", new { typeOfAccount = "Joint", jointCustomerId = partner.CustomerId }, admin.Token);

        Assert.Equal(400, bad.Code);
        good.AssertOk("joint submit");
    }
}
