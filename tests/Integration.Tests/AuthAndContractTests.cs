using System.Net;

namespace Integration.Tests;

// Identity's tokens and the other three services trusting them; Risk's output being accepted by Review.
public class AuthAndContractTests
{
    private static readonly string[] AllFlags = { "VerificationFailed", "OfacMatch", "ClosedJurisdiction", "ProhibitedIndustry" };
    public static IEnumerable<object[]> EveryCombination() => Enumerable.Range(0, 16).Select(mask => new object[] { mask });

    // ---- the stack is up ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("identity")] [InlineData("intake")] [InlineData("risk")] [InlineData("review")]
    public async Task EveryServiceIsHealthy(string service)
    {
        var url = service switch { "identity" => Stack.IdentityUrl, "intake" => Stack.IntakeUrl, "risk" => Stack.RiskUrl, _ => Stack.ReviewUrl };

        (await Stack.Get($"{url}/health")).AssertOk(service);
    }

    [Fact] public async Task ReviewIsReadyOnceItsDatabaseIsUp() => (await Stack.Get($"{Stack.ReviewUrl}/health/ready")).AssertOk("review ready");

    [Fact]
    public async Task IdentityPublishesItsSigningKeyAndDiscoveryDocument()
    {
        var jwks = await Stack.Get($"{Stack.IdentityUrl}/.well-known/jwks.json");
        var discovery = await Stack.Get($"{Stack.IdentityUrl}/.well-known/openid-configuration");

        jwks.AssertOk("jwks");
        Assert.Equal("RSA", jwks.Json.GetProperty("keys")[0].GetProperty("kty").GetString());
        Assert.False(jwks.Json.GetProperty("keys")[0].TryGetProperty("d", out _), "the private exponent must never be published");
        discovery.AssertOk("discovery");
        Assert.EndsWith("/.well-known/jwks.json", discovery.Json.GetProperty("jwks_uri").GetString());
    }

    // ---- who can call what -----------------------------------------------------------------------------------

    [Fact]
    public async Task EveryProtectedEndpoint_RefusesACallWithoutAToken()
    {
        Assert.Equal(401, (await Stack.Get($"{Stack.ReviewUrl}/api/admins/tickets")).Code);
        Assert.Equal(401, (await Stack.Get($"{Stack.IntakeUrl}/api/admins/pending-customers")).Code);
        Assert.Equal(401, (await Stack.Post($"{Stack.IntakeUrl}/api/admins/inputForm", new CustomerForm().ToBody())).Code);
        Assert.Equal(401, (await Stack.Post($"{Stack.RiskUrl}/internal/analyze", new { })).Code);
        Assert.Equal(401, (await Stack.Post($"{Stack.ReviewUrl}/internal/tickets", new { })).Code);
        Assert.Equal(401, (await Stack.Get($"{Stack.IdentityUrl}/api/users/me")).Code);
    }

    [Fact]
    public async Task AGarbageToken_IsRefusedEverywhere()
    {
        Assert.Equal(401, (await Stack.Get($"{Stack.ReviewUrl}/api/admins/tickets", "not.a.token")).Code);
        Assert.Equal(401, (await Stack.Get($"{Stack.IntakeUrl}/api/admins/pending-customers", "not.a.token")).Code);
        Assert.Equal(401, (await Stack.Post($"{Stack.RiskUrl}/internal/analyze", new { }, "not.a.token")).Code);
    }

    [Fact]
    public async Task ACustomersToken_CannotUseAdminEndpoints()
    {
        var admin = await Reviewers.One();
        var customer = await Stack.Onboard(admin);
        var login = await Stack.Post($"{Stack.IdentityUrl}/api/users/login", new { username = customer.Form.Username, password = Stack.Password });
        login.AssertOk("customer login");
        var token = login.Json.GetProperty("token").GetString()!;

        Assert.Equal(403, (await Stack.Get($"{Stack.ReviewUrl}/api/admins/tickets", token)).Code);
        Assert.Equal(403, (await Stack.Get($"{Stack.IntakeUrl}/api/admins/pending-customers", token)).Code);
        Assert.Equal(403, (await Stack.Post($"{Stack.RiskUrl}/internal/analyze", new { }, token)).Code);
        (await Stack.Get($"{Stack.IdentityUrl}/api/users/me", token)).AssertOk("customer me");
    }

    [Fact]
    public async Task InternalAccountEndpoints_NeedTheSharedKey()
    {
        var url = $"{Stack.IdentityUrl}/internal/accounts/username-available?role=admin&username=x";

        Assert.Equal(401, (await Stack.Get(url)).Code);
        Assert.Equal(401, (await Stack.Send(HttpMethod.Get, url, internalKey: "wrong-key")).Code);
        Assert.Equal(401, (await Stack.Send(HttpMethod.Post, $"{Stack.IdentityUrl}/internal/accounts", new { }, internalKey: "wrong-key")).Code);
    }

    [Fact]
    public async Task InternalKey_FromTheEnvironment_ReportsUsernameAvailability()
    {
        var key = Environment.GetEnvironmentVariable("INTERNAL_API_KEY");
        if (string.IsNullOrEmpty(key)) return; // only when the runner knows the stack's key
        var admin = await Reviewers.One();

        var taken = await Stack.Send(HttpMethod.Get, $"{Stack.IdentityUrl}/internal/accounts/username-available?role=admin&username={admin.Username}", internalKey: key);
        var free = await Stack.Send(HttpMethod.Get, $"{Stack.IdentityUrl}/internal/accounts/username-available?role=admin&username={Stack.Unique("free")}", internalKey: key);

        Assert.False(taken.Json.GetProperty("available").GetBoolean());
        Assert.True(free.Json.GetProperty("available").GetBoolean());
    }

    // ---- login, lockout, recovery, logout ------------------------------------------------------------------------

    [Fact]
    public async Task WrongPasswordIs401_UnknownUserLooksTheSame()
    {
        var admin = await Reviewers.One();

        var wrong = await Stack.Post($"{Stack.IdentityUrl}/api/admins/login", new { username = admin.Username, password = "Wrong!Pass1" });
        var unknown = await Stack.Post($"{Stack.IdentityUrl}/api/admins/login", new { username = Stack.Unique("nobody"), password = "Wrong!Pass1" });

        Assert.Equal(401, wrong.Code);
        Assert.Equal(wrong.Body, unknown.Body);
    }

    [Fact]
    public async Task ThreeWrongPasswords_LockTheAccount_EvenForTheRightPassword()
    {
        var admin = await Stack.NewAdmin();
        var login = (string password) => Stack.Post($"{Stack.IdentityUrl}/api/admins/login", new { username = admin.Username, password });

        Assert.Equal(401, (await login("Wrong!Pass1")).Code);
        Assert.Equal(401, (await login("Wrong!Pass2")).Code);
        Assert.Equal(423, (await login("Wrong!Pass3")).Code);
        var locked = await login(Stack.Password);

        Assert.Equal(423, locked.Code);
        Assert.Contains("locked", locked.Body);
    }

    [Fact]
    public async Task PasswordRecovery_NeedsTheRightAnswer_AndThenTheNewPasswordWorks()
    {
        var admin = await Stack.NewAdmin();
        var question = await Stack.Get($"{Stack.IdentityUrl}/api/admins/recovery-question?username={admin.Username}");
        Assert.Equal(Stack.Question, question.Json.GetProperty("question").GetString());

        var wrong = await Stack.Post($"{Stack.IdentityUrl}/api/admins/reset-password", new { username = admin.Username, answer = "pasta", newPassword = "N3w!Password" });
        Assert.Equal(401, wrong.Code);

        var right = await Stack.Post($"{Stack.IdentityUrl}/api/admins/reset-password", new { username = admin.Username, answer = " PIZZA ", newPassword = "N3w!Password" });
        right.AssertOk("reset");
        Assert.Equal(401, (await Stack.Post($"{Stack.IdentityUrl}/api/admins/login", new { username = admin.Username, password = Stack.Password })).Code);
        await Stack.LoginAdmin(admin.Username, "N3w!Password");
    }

    [Fact]
    public async Task RecoveryQuestion_ForAnUnknownUser_IsADecoy_NotAnError()
    {
        var reply = await Stack.Get($"{Stack.IdentityUrl}/api/admins/recovery-question?username={Stack.Unique("ghost")}");

        reply.AssertOk("decoy");
        Assert.False(string.IsNullOrEmpty(reply.Json.GetProperty("question").GetString()));
    }

    [Fact]
    public async Task Logout_RevokesTheToken_SoIdentityRefusesItAfterwards()
    {
        var admin = await Stack.NewAdmin();

        (await Stack.Post($"{Stack.IdentityUrl}/api/admins/logout", token: admin.Token)).AssertOk("logout");

        Assert.Equal(401, (await Stack.Post($"{Stack.IdentityUrl}/api/admins/logout", token: admin.Token)).Code);
    }

    [Fact]
    public async Task AdminSignup_RefusesATakenUsername()
    {
        var admin = await Reviewers.One();

        var again = await Stack.Post($"{Stack.IntakeUrl}/api/admins/signup", new
        {
            username = admin.Username, password = Stack.Password, passwordRecoveryQuestion = Stack.Question, passwordRecoveryAnswer = Stack.Answer,
            firstName = "Eve", lastName = "Other", dateOfBirth = "1985-03-03", gender = "Female", ssn = Stack.Digits(9), isCitizen = true, countryOfCitizenship = "US",
        });

        Assert.Equal(HttpStatusCode.Conflict, again.Status);
    }

    // ---- Risk -> Review contract: whatever Risk produces, Review must accept it ---------------------------------------

    private static Action<CustomerForm> Raising(int mask) => CustomerForm.Raising(AllFlags.Where((_, i) => (mask & (1 << i)) != 0).ToArray());

    [Theory]
    [MemberData(nameof(EveryCombination))]
    public async Task RisksAssessment_ForEveryFlagCombination_IsAcceptedAsIsByReview(int mask)
    {
        var admin = await Reviewers.One();
        var form = new CustomerForm();
        Raising(mask)(form);
        var digits = form.TaxIdNumber;
        var risk = await Stack.Post($"{Stack.RiskUrl}/internal/analyze", new
        {
            firstName = form.FirstName, middleName = form.MiddleName.Length == 0 ? null : form.MiddleName, lastName = form.LastName,
            taxIdLastFourDigits = digits[^4..], residentialAddress = form.Address,
            countryOfResidence = form.Residence, countryOfCitizenship = form.Citizenship, occupation = form.Occupation, employer = form.Employer,
        }, admin.Token);
        risk.AssertOk("risk analyze");
        var assessment = risk.Json;

        var ticket = await Stack.Post($"{Stack.ReviewUrl}/internal/tickets", new
        {
            requestId = Guid.NewGuid(), customerId = int.Parse(Stack.Digits(8)) + 1, accountType = "Individual",
            customer = new
            {
                firstName = form.FirstName, middleName = form.MiddleName.Length == 0 ? null : form.MiddleName, lastName = form.LastName, dateOfBirth = "1990-01-01T00:00:00",
                gender = "Male", taxIdType = "SSN", residentialAddress = form.Address, countryOfResidence = form.Residence,
                countryOfCitizenship = form.Citizenship, occupation = form.Occupation, employer = form.Employer,
            },
            riskRating = assessment.GetProperty("riskRating").GetString(),
            flags = assessment.GetProperty("flags"),
            vendorResponse = assessment.GetProperty("vendorResponse"),
        }, admin.Token);

        ticket.AssertOk($"review accepting risk output {assessment}");
    }

    [Theory]
    [InlineData("OfacMatch", "High")]
    [InlineData("VerificationFailed", "Medium")]
    public async Task ReviewRefuses_ARatingThatDoesNotMatchTheFlags(string flag, string wrongRating)
    {
        var admin = await Reviewers.One();
        var wrong = wrongRating == "High" ? "Low" : "High";

        var reply = await Stack.Post($"{Stack.ReviewUrl}/internal/tickets", new
        {
            requestId = Guid.NewGuid(), customerId = int.Parse(Stack.Digits(8)) + 1, accountType = "Individual",
            customer = new { firstName = "A", lastName = "B", dateOfBirth = "1990-01-01T00:00:00", gender = "Male", taxIdType = "SSN", residentialAddress = "x", countryOfResidence = "US", countryOfCitizenship = "US", occupation = "x", employer = "x" },
            riskRating = wrong, flags = new[] { flag },
            vendorResponse = new { nameMatch = "Match", dateOfBirthMatch = "Match", addressMatch = "Match", taxIdMatch = "Match", score = flag == "VerificationFailed" ? 30 : 50, riskCodes = flag == "VerificationFailed" ? "DeceasedSSN" : "" },
        }, admin.Token);

        Assert.Equal(400, reply.Code);
    }

    [Fact]
    public async Task ReplayingATicketRequest_ReturnsTheSameTicket_AndAChangedOneIsAConflict()
    {
        var admin = await Reviewers.One();
        var requestId = Guid.NewGuid();
        var customerId = int.Parse(Stack.Digits(8)) + 1;
        object Body(string occupation) => new
        {
            requestId, customerId, accountType = "Individual",
            customer = new { firstName = "A", lastName = "B", dateOfBirth = "1990-01-01T00:00:00", gender = "Male", taxIdType = "SSN", residentialAddress = "x", countryOfResidence = "US", countryOfCitizenship = "US", occupation, employer = "x" },
            riskRating = "Low", flags = Array.Empty<string>(),
            vendorResponse = new { nameMatch = "Match", dateOfBirthMatch = "Match", addressMatch = "Match", taxIdMatch = "Match", score = 50, riskCodes = "" },
        };

        var first = await Stack.Post($"{Stack.ReviewUrl}/internal/tickets", Body("Teacher"), admin.Token);
        var replay = await Stack.Post($"{Stack.ReviewUrl}/internal/tickets", Body("Teacher"), admin.Token);
        var changed = await Stack.Post($"{Stack.ReviewUrl}/internal/tickets", Body("Pilot"), admin.Token);

        first.AssertOk("first");
        replay.AssertOk("replay");
        Assert.Equal(first.Json.GetProperty("ticketId").GetInt32(), replay.Json.GetProperty("ticketId").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, changed.Status);
    }
}
