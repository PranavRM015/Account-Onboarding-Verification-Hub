using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Intake.Controllers;
using Intake.Data;
using Intake.DTOs;
using Intake.Models;
using Intake.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Intake.Tests;

// Stands in for the other services: answers each request with whatever the test decides and remembers what was sent.
internal sealed class Stub : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Calls { get; } = new();
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _answer;
    public Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) => _answer = answer;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request, body));
        return _answer(request);
    }

    public static Stub Returning(HttpStatusCode status, object? json = null) => new(_ => new HttpResponseMessage(status)
    {
        Content = json is null ? new StringContent("") : JsonContent.Create(json),
    });

    public static Stub Throwing() => new(_ => throw new HttpRequestException("connection refused"));
    public static HttpClient Client(Stub stub) => new(stub) { BaseAddress = new Uri("http://stub") };
}

public class ReviewServiceClientTests
{
    private static ReviewServiceClient Client(Stub stub) => new(Stub.Client(stub));

    [Fact]
    public async Task OnboardedIds_AreReturned_AndTheAdminTokenIsForwarded()
    {
        var stub = Stub.Returning(HttpStatusCode.OK, new[] { 3, 5, 8 });

        var ids = await Client(stub).GetOnboardedCustomerIdsAsync("tok");

        Assert.Equal(new[] { 3, 5, 8 }, ids);
        Assert.Equal("Bearer tok", stub.Calls.Single().Request.Headers.Authorization!.ToString());
        Assert.Equal("/internal/tickets/onboarded-customer-ids", stub.Calls.Single().Request.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task OnboardedIds_AreNull_WhenReviewAnswersWithAnError_NeverAnEmptyList(HttpStatusCode status) =>
        Assert.Null(await Client(Stub.Returning(status)).GetOnboardedCustomerIdsAsync("tok"));

    [Fact] public async Task OnboardedIds_AreNull_WhenReviewIsUnreachable() => Assert.Null(await Client(Stub.Throwing()).GetOnboardedCustomerIdsAsync("tok"));

    [Fact]
    public async Task FindTicket_Found_ReturnsTheTicket()
    {
        var result = await Client(Stub.Returning(HttpStatusCode.OK, new { ticketId = 12, customerId = 3 })).FindTicketByCustomerAsync(3, "tok");

        Assert.True(result.IsFound);
        Assert.Equal(12, result.Ticket!.TicketId);
    }

    [Fact] public async Task FindTicket_404_MeansDefinitelyNotFound() => Assert.True((await Client(Stub.Returning(HttpStatusCode.NotFound)).FindTicketByCustomerAsync(3, "tok")).IsNotFound);

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)] [InlineData(HttpStatusCode.Unauthorized)] [InlineData(HttpStatusCode.Forbidden)]
    public async Task FindTicket_OtherErrors_AreFailed_NotNotFound(HttpStatusCode status) =>
        Assert.True((await Client(Stub.Returning(status)).FindTicketByCustomerAsync(3, "tok")).IsFailed);

    [Fact] public async Task FindTicket_NetworkError_IsFailed() => Assert.True((await Client(Stub.Throwing()).FindTicketByCustomerAsync(3, "tok")).IsFailed);

    [Fact]
    public async Task CreateTicket_PostsTheRequestWithTheForwardedToken()
    {
        var stub = Stub.Returning(HttpStatusCode.OK, new { ticketId = 1 });

        await Client(stub).CreateTicketAsync(new ReviewCreateTicketDto { CustomerId = 4, Flags = new[] { "OfacMatch" } }, "tok");

        var call = stub.Calls.Single();
        Assert.Equal(HttpMethod.Post, call.Request.Method);
        Assert.Equal("/internal/tickets", call.Request.RequestUri!.AbsolutePath);
        Assert.Contains("OfacMatch", call.Body);
    }
}

public class IdentityAndRiskClientTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Internal:ApiKey"] = "k" }).Build();

    [Fact]
    public void IdentityClient_WithoutAnInternalKey_RefusesToStart() =>
        Assert.Throws<InvalidOperationException>(() => new IdentityServiceClient(Stub.Client(Stub.Returning(HttpStatusCode.OK)), new ConfigurationBuilder().Build()));

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task UsernameAvailable_ReportsWhatIdentitySays(bool available)
    {
        var stub = Stub.Returning(HttpStatusCode.OK, new { available });

        Assert.Equal(available, await new IdentityServiceClient(Stub.Client(stub), Config()).IsUsernameAvailable("admin", "a b"));

        var request = stub.Calls.Single().Request;
        Assert.Equal("k", request.Headers.GetValues("X-Internal-Key").Single());
        Assert.Contains("role=admin", request.RequestUri!.Query);
        Assert.Contains("username=a%20b", request.RequestUri.Query);
    }

    [Fact] public async Task UsernameAvailable_IsNull_WhenIdentityFails() => Assert.Null(await new IdentityServiceClient(Stub.Client(Stub.Returning(HttpStatusCode.InternalServerError)), Config()).IsUsernameAvailable("admin", "a"));
    [Fact] public async Task UsernameAvailable_IsNull_WhenIdentityIsUnreachable() => Assert.Null(await new IdentityServiceClient(Stub.Client(Stub.Throwing()), Config()).IsUsernameAvailable("admin", "a"));

    [Fact]
    public async Task CreateAccount_SendsTheKeyAndTheCredentials()
    {
        var stub = Stub.Returning(HttpStatusCode.OK);

        await new IdentityServiceClient(Stub.Client(stub), Config()).CreateAccount("customer", 5, "carol", "Pw!1aaaa", "Q?", "A");

        var call = stub.Calls.Single();
        Assert.Equal("k", call.Request.Headers.GetValues("X-Internal-Key").Single());
        var body = JsonDocument.Parse(call.Body).RootElement;
        Assert.Equal("customer", body.GetProperty("role").GetString());
        Assert.Equal(5, body.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task RiskClient_ForwardsTheTokenAndProfile()
    {
        var stub = Stub.Returning(HttpStatusCode.OK, new { });

        await new RiskServiceClient(Stub.Client(stub)).AnalyzeAsync(new RiskAnalyzeRequest { FirstName = "Zed" }, "tok");

        var call = stub.Calls.Single();
        Assert.Equal("/internal/analyze", call.Request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer tok", call.Request.Headers.Authorization!.ToString());
        Assert.Contains("Zed", call.Body);
    }
}

// The admin controller with the other three services stubbed out and an in-memory database.
public class AdminIntakeControllerTests
{
    private sealed class Rig
    {
        public IntakeDbContext Db = new(new DbContextOptionsBuilder<IntakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Stub Review = Stub.Returning(HttpStatusCode.NotFound);
        public Stub Risk = Stub.Returning(HttpStatusCode.OK, new RiskAnalyzeResponse { RiskRating = "Low", VendorResponse = new RiskVendorResult { NameMatch = "Match", DateOfBirthMatch = "Match", AddressMatch = "Match", TaxIdMatch = "Match", Score = 50 } });
        public Stub Identity = Stub.Returning(HttpStatusCode.OK);

        public AdminIntakeController Controller(string? bearer = "tok", string? user = "alice")
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["TaxIdHashPepper"] = "pepper", ["Internal:ApiKey"] = "k" }).Build();
            var controller = new AdminIntakeController(Db, new IdentityServiceClient(Stub.Client(Identity), config), new RiskServiceClient(Stub.Client(Risk)),
                new ReviewServiceClient(Stub.Client(Review)), config, NullLogger<AdminIntakeController>.Instance);
            var http = new DefaultHttpContext();
            if (bearer is not null) http.Request.Headers.Authorization = $"Bearer {bearer}";
            if (user is not null) http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("name", user) }, "test", "name", "role"));
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            return controller;
        }

        public int AddCustomer(string first, DateTime? created = null)
        {
            var customer = new CustomerProfile
            {
                FirstName = first, LastName = "Customer", DateOfBirth = new DateTime(1990, 1, 1), Gender = "Male", TaxIdType = "SSN",
                TaxIdHash = TaxIdHasher.Hash(first + Guid.NewGuid(), "pepper"), TaxIdLastFourDigits = "1234", ResidentialAddress = "1 Main Street",
                CountryOfResidence = "US", CountryOfCitizenship = "US", Occupation = "Teacher", Employer = "School",
                CreatedAt = created ?? DateTime.UtcNow, Username = first.ToLowerInvariant(),
            };
            Db.Customers.Add(customer); Db.SaveChanges();
            return customer.CustomerId;
        }
    }

    private static T Body<T>(IActionResult result) where T : class => Assert.IsType<T>(Assert.IsAssignableFrom<ObjectResult>(result).Value);
    private static List<int> Ids(IActionResult result)
    {
        var rows = (System.Collections.IEnumerable)Assert.IsType<OkObjectResult>(result).Value!;
        return rows.Cast<object>().Select(r => (int)r.GetType().GetProperty("customerId")!.GetValue(r)!).ToList();
    }

    // ---- GET pending-customers ---------------------------------------------------------------------

    [Fact]
    public async Task PendingList_HidesCustomersWhoAlreadyHaveATicket_AndListsTheOldestFirst()
    {
        var rig = new Rig();
        var older = rig.AddCustomer("Older", DateTime.UtcNow.AddDays(-3));
        var onboarded = rig.AddCustomer("Done", DateTime.UtcNow.AddDays(-2));
        var newer = rig.AddCustomer("Newer", DateTime.UtcNow.AddDays(-1));
        rig.Review = Stub.Returning(HttpStatusCode.OK, new[] { onboarded });

        var result = await rig.Controller().ListPending();

        Assert.Equal(new[] { older, newer }, Ids(result));
    }

    [Fact]
    public async Task PendingList_IsEmpty_WhenEveryoneIsOnboarded()
    {
        var rig = new Rig();
        var only = rig.AddCustomer("Only");
        rig.Review = Stub.Returning(HttpStatusCode.OK, new[] { only });

        Assert.Empty(Ids(await rig.Controller().ListPending()));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task PendingList_Is503_WhenReviewCannotConfirmWhoIsOnboarded_NotAnEmptyOrFullList(HttpStatusCode reviewStatus)
    {
        var rig = new Rig();
        rig.AddCustomer("Someone");
        rig.Review = Stub.Returning(reviewStatus);

        var result = await rig.Controller().ListPending();

        Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task PendingList_Is503_WhenReviewIsUnreachable()
    {
        var rig = new Rig();
        rig.Review = Stub.Throwing();

        Assert.Equal(503, Assert.IsType<ObjectResult>(await rig.Controller().ListPending()).StatusCode);
    }

    [Fact] public async Task PendingList_NeedsABearerToken() => Assert.IsType<UnauthorizedObjectResult>(await new Rig().Controller(bearer: null).ListPending());

    // ---- POST pending-customers/{id}/submit: each flag Risk raises reaches Review -------------------

    public static IEnumerable<object[]> FlagScenarios() => new[]
    {
        new object[] { new[] { "VerificationFailed" }, "Medium", 30, "DeceasedSSN" },
        new object[] { new[] { "OfacMatch" }, "High", 50, "" },
        new object[] { new[] { "ClosedJurisdiction" }, "Medium", 50, "" },
        new object[] { new[] { "ProhibitedIndustry" }, "High", 50, "" },
        new object[] { new[] { "VerificationFailed", "OfacMatch", "ClosedJurisdiction", "ProhibitedIndustry" }, "High", 30, "DeceasedSSN" },
        new object[] { Array.Empty<string>(), "Low", 50, "" },
    };

    [Theory]
    [MemberData(nameof(FlagScenarios))]
    public async Task Submit_PassesRisksFlagsRatingAndVendorFindings_ToReviewUnchanged(string[] flags, string rating, int score, string riskCodes)
    {
        var rig = new Rig();
        var id = rig.AddCustomer("Pending");
        rig.Risk = Stub.Returning(HttpStatusCode.OK, new RiskAnalyzeResponse
        {
            RiskRating = rating, Flags = flags,
            VendorResponse = new RiskVendorResult { NameMatch = "Match", DateOfBirthMatch = "Match", AddressMatch = "Match", TaxIdMatch = "Match", Score = score, RiskCodes = riskCodes },
        });
        rig.Review = new Stub(request => request.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { ticketId = 77, customerId = id }) });

        var result = await rig.Controller().Submit(id, new PendingSubmitDto { typeOfAccount = "Individual" });

        Assert.IsType<OkObjectResult>(result);
        var sent = JsonDocument.Parse(rig.Review.Calls.Single(c => c.Request.Method == HttpMethod.Post).Body).RootElement;
        Assert.Equal(flags, sent.GetProperty("flags").EnumerateArray().Select(f => f.GetString()).ToArray());
        Assert.Equal(rating, sent.GetProperty("riskRating").GetString());
        Assert.Equal(score, sent.GetProperty("vendorResponse").GetProperty("score").GetInt32());
        Assert.Equal(riskCodes, sent.GetProperty("vendorResponse").GetProperty("riskCodes").GetString());
        Assert.Equal(id, sent.GetProperty("customerId").GetInt32());
    }

    [Fact]
    public async Task Submit_ForACustomerWhoAlreadyHasATicket_IsAConflict_AndNeverCallsRisk()
    {
        var rig = new Rig();
        var id = rig.AddCustomer("Pending");
        rig.Review = Stub.Returning(HttpStatusCode.OK, new { ticketId = 5, customerId = id });

        Assert.IsType<ConflictObjectResult>(await rig.Controller().Submit(id, new PendingSubmitDto { typeOfAccount = "Individual" }));
        Assert.Empty(rig.Risk.Calls);
    }

    [Fact]
    public async Task Submit_Is503_WhenReviewCannotSayWhetherATicketExists()
    {
        var rig = new Rig();
        var id = rig.AddCustomer("Pending");
        rig.Review = Stub.Returning(HttpStatusCode.InternalServerError);

        Assert.Equal(503, Assert.IsType<ObjectResult>(await rig.Controller().Submit(id, new PendingSubmitDto { typeOfAccount = "Individual" })).StatusCode);
    }

    [Theory]
    [InlineData("Business")] [InlineData("")]
    public async Task Submit_NeedsAKnownAccountType(string type) =>
        Assert.IsType<BadRequestObjectResult>(await new Rig().Controller().Submit(1, new PendingSubmitDto { typeOfAccount = type }));

    [Fact]
    public async Task Submit_JointAccount_NeedsAnExistingSecondCustomer()
    {
        var rig = new Rig();
        var id = rig.AddCustomer("First");

        Assert.IsType<BadRequestObjectResult>(await rig.Controller().Submit(id, new PendingSubmitDto { typeOfAccount = "Joint", jointCustomerId = 999 }));
        Assert.IsType<BadRequestObjectResult>(await rig.Controller().Submit(id, new PendingSubmitDto { typeOfAccount = "Joint" }));
    }

    [Fact]
    public async Task Submit_ForAnUnknownCustomer_Is404() =>
        Assert.IsType<NotFoundObjectResult>(await new Rig().Controller().Submit(404, new PendingSubmitDto { typeOfAccount = "Individual" }));

    // ---- risk failures map to the right status, before anything is saved ------------------------------

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    [InlineData(HttpStatusCode.InternalServerError, 502)]
    public async Task Submit_TranslatesARiskFailure(HttpStatusCode risk, int expected)
    {
        var rig = new Rig();
        var id = rig.AddCustomer("Pending");
        rig.Risk = Stub.Returning(risk);

        var result = await rig.Controller().Submit(id, new PendingSubmitDto { typeOfAccount = "Individual" });

        Assert.Equal(expected, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.DoesNotContain(rig.Review.Calls, c => c.Request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Submit_Is503_WhenRiskIsUnreachable()
    {
        var rig = new Rig();
        var id = rig.AddCustomer("Pending");
        rig.Risk = Stub.Throwing();

        Assert.Equal(503, Assert.IsType<ObjectResult>(await rig.Controller().Submit(id, new PendingSubmitDto { typeOfAccount = "Individual" })).StatusCode);
    }

    // ---- input form ---------------------------------------------------------------------------------------

    private static InputFormDto Form(Action<InputFormDto>? change = null)
    {
        var dto = new InputFormDto
        {
            FirstName = "Zed", LastName = "Quillfeather", DateOfBirth = new DateTime(1990, 1, 1), Gender = "Male",
            taxIdType = "SSN", taxIdNumber = "123-45-6789", residentialAddress = "1 Main Street",
            countryOfResidence = "US", countryOfCitizenship = "US", occupation = "Teacher", Employer = "School",
            typeOfAccount = "Individual", username = "zed", password = "Str0ng!Pass",
            passwordRecoveryQuestion = "What is your favorite food?", passwordRecoveryAnswer = "pizza",
        };
        change?.Invoke(dto);
        return dto;
    }

    [Theory]
    [InlineData("Business")] [InlineData("")]
    public async Task InputForm_NeedsAKnownAccountType(string type) =>
        Assert.IsType<BadRequestObjectResult>(await new Rig().Controller().InputForm(Form(f => f.typeOfAccount = type)));

    [Fact] public async Task InputForm_NeedsATaxId() => Assert.IsType<BadRequestObjectResult>(await new Rig().Controller().InputForm(Form(f => f.taxIdNumber = " ")));
    [Fact] public async Task InputForm_NeedsCredentials()
    {
        var rig = new Rig();
        Assert.IsType<BadRequestObjectResult>(await rig.Controller().InputForm(Form(f => f.username = "")));
        Assert.IsType<BadRequestObjectResult>(await rig.Controller().InputForm(Form(f => f.password = "")));
        Assert.IsType<BadRequestObjectResult>(await rig.Controller().InputForm(Form(f => f.passwordRecoveryQuestion = "")));
        Assert.IsType<BadRequestObjectResult>(await rig.Controller().InputForm(Form(f => f.passwordRecoveryAnswer = "")));
    }
    [Fact] public async Task InputForm_JointAccount_NeedsAnExistingSecondCustomer() =>
        Assert.IsType<BadRequestObjectResult>(await new Rig().Controller().InputForm(Form(f => { f.typeOfAccount = "Joint"; f.jointCustomerId = 999; })));

    [Fact]
    public async Task InputForm_SendsTheLastFourTaxDigitsToRisk_NotTheWholeNumber()
    {
        var rig = new Rig();
        rig.Identity = Stub.Returning(HttpStatusCode.OK);
        rig.Review = new Stub(r => r.Method == HttpMethod.Get ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { ticketId = 1 }) });

        await rig.Controller().InputForm(Form());

        var sentToRisk = rig.Risk.Calls.Single().Body;
        Assert.Contains("\"taxIdLastFourDigits\":\"6789\"", sentToRisk);
        Assert.DoesNotContain("123-45-6789", sentToRisk);
        Assert.DoesNotContain("123456789", sentToRisk);
    }

    [Fact]
    public async Task InputForm_SavesNothing_WhenRiskFails()
    {
        var rig = new Rig();
        rig.Risk = Stub.Returning(HttpStatusCode.ServiceUnavailable);

        await rig.Controller().InputForm(Form());

        Assert.Empty(rig.Db.Customers);
        Assert.Empty(rig.Identity.Calls);
    }

    [Fact]
    public async Task InputForm_RemovesTheProfile_WhenIdentityRejectsTheCredentials()
    {
        var rig = new Rig();
        rig.Identity = Stub.Returning(HttpStatusCode.BadRequest);

        var result = await rig.Controller().InputForm(Form());

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(rig.Db.Customers);
    }

    [Fact]
    public async Task InputForm_KeepsTheProfile_WhenIdentityBreaksHalfWay()
    {
        var rig = new Rig();
        rig.Identity = Stub.Returning(HttpStatusCode.InternalServerError);

        var result = await rig.Controller().InputForm(Form());

        Assert.Equal(503, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Single(rig.Db.Customers);
    }

    [Fact]
    public async Task CustomerExists_ReportsWhetherTheProfileIsThere()
    {
        var rig = new Rig();
        var id = rig.AddCustomer("Here");

        Assert.True(Exists(rig.Controller().CustomerExists(id)));
        Assert.False(Exists(rig.Controller().CustomerExists(id + 100)));
    }

    private static bool Exists(IActionResult result) =>
        (bool)Assert.IsType<OkObjectResult>(result).Value!.GetType().GetProperty("exists")!.GetValue(Assert.IsType<OkObjectResult>(result).Value)!;
}

// Self-signup controllers: only the checks that happen before anything is saved.
public class SignupControllerTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["TaxIdHashPepper"] = "pepper", ["Internal:ApiKey"] = "k" }).Build();
    private static IntakeDbContext NewDb() => new(new DbContextOptionsBuilder<IntakeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CustomerIntakeController Customers(Stub identity, IntakeDbContext db) =>
        new(db, new IdentityServiceClient(Stub.Client(identity), Config()), Config(), NullLogger<CustomerIntakeController>.Instance);

    private static AdminSignupController Admins(Stub identity, IntakeDbContext db) =>
        new(db, new IdentityServiceClient(Stub.Client(identity), Config()), Config(), NullLogger<AdminSignupController>.Instance);

    private static CustomerSignupDto Customer(Action<CustomerSignupDto>? change = null)
    {
        var dto = new CustomerSignupDto
        {
            username = "carol", password = "Str0ng!Pass", passwordRecoveryQuestion = "What is your favorite food?", passwordRecoveryAnswer = "pizza",
            firstName = "Carol", lastName = "Customer", dateOfBirth = new DateTime(1990, 1, 1), gender = "Female", taxIdType = "SSN", taxIdNumber = "123456789",
            residentialAddress = "1 Main Street", countryOfResidence = "US", countryOfCitizenship = "US", occupation = "Teacher", employer = "School",
        };
        change?.Invoke(dto);
        return dto;
    }

    private static AdminSignupDto Admin(Action<AdminSignupDto>? change = null)
    {
        var dto = new AdminSignupDto
        {
            username = "alice", password = "Str0ng!Pass", passwordRecoveryQuestion = "What is your favorite food?", passwordRecoveryAnswer = "pizza",
            firstName = "Alice", lastName = "Admin", dateOfBirth = new DateTime(1985, 1, 1), gender = "Female", ssn = "123456789", isCitizen = true, countryOfCitizenship = "US",
        };
        change?.Invoke(dto);
        return dto;
    }

    [Fact]
    public async Task CustomerSignup_RejectsAnInvalidProfile_BeforeTouchingIdentityOrTheDatabase()
    {
        var identity = Stub.Returning(HttpStatusCode.OK, new { available = true });
        var db = NewDb();

        var result = await Customers(identity, db).Signup(Customer(c => c.gender = "x"));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(identity.Calls);
        Assert.Empty(db.Customers);
    }

    [Fact]
    public async Task CustomerSignup_NeedsEveryCredentialField()
    {
        var controller = Customers(Stub.Returning(HttpStatusCode.OK, new { available = true }), NewDb());

        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Customer(c => c.username = " ")));
        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Customer(c => c.password = "")));
        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Customer(c => c.passwordRecoveryQuestion = "")));
        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Customer(c => c.passwordRecoveryAnswer = "")));
    }

    [Fact]
    public async Task CustomerSignup_ATakenUsername_IsAConflict_AndNothingIsSaved()
    {
        var db = NewDb();

        var result = await Customers(Stub.Returning(HttpStatusCode.OK, new { available = false }), db).Signup(Customer());

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(db.Customers);
    }

    [Fact]
    public async Task AdminSignup_RejectsAnInvalidProfile_BeforeTouchingIdentity()
    {
        var identity = Stub.Returning(HttpStatusCode.OK, new { available = true });

        Assert.IsType<BadRequestObjectResult>(await Admins(identity, NewDb()).Signup(Admin(a => a.ssn = "123")));
        Assert.Empty(identity.Calls);
    }

    [Fact]
    public async Task AdminSignup_NeedsEveryCredentialField()
    {
        var controller = Admins(Stub.Returning(HttpStatusCode.OK, new { available = true }), NewDb());

        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Admin(a => a.username = "")));
        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Admin(a => a.password = "")));
        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Admin(a => a.passwordRecoveryQuestion = "")));
        Assert.IsType<BadRequestObjectResult>(await controller.Signup(Admin(a => a.passwordRecoveryAnswer = "")));
    }

    [Fact]
    public async Task AdminSignup_ATakenUsername_IsAConflict_AndNothingIsSaved()
    {
        var db = NewDb();

        Assert.IsType<ConflictObjectResult>(await Admins(Stub.Returning(HttpStatusCode.OK, new { available = false }), db).Signup(Admin()));
        Assert.Empty(db.Admins);
    }

    [Fact]
    public void ControllersRefuseToStart_WithoutThePepper()
    {
        var empty = new ConfigurationBuilder().Build();
        var identity = new IdentityServiceClient(Stub.Client(Stub.Returning(HttpStatusCode.OK)), Config());

        Assert.Throws<InvalidOperationException>(() => new CustomerIntakeController(NewDb(), identity, empty, NullLogger<CustomerIntakeController>.Instance));
        Assert.Throws<InvalidOperationException>(() => new AdminSignupController(NewDb(), identity, empty, NullLogger<AdminSignupController>.Instance));
    }
}
