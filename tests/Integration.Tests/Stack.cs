using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Integration.Tests;

// Talks to the whole stack the way the Angular app does: only over HTTP, through the real services and real SQL Server.
// Start it first:  docker compose -f docker-compose.yml -f docker-compose.ci.yml up -d --build
public static class Stack
{
    private static string Env(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : fallback;

    public static readonly string IdentityUrl = Env("IDENTITY_URL", "http://localhost:5290");
    public static readonly string IntakeUrl = Env("INTAKE_URL", "http://localhost:5168");
    public static readonly string RiskUrl = Env("RISK_URL", "http://localhost:5291");
    public static readonly string ReviewUrl = Env("REVIEW_URL", "http://localhost:5292");

    // Direct access to the Review database, used only to age tickets and to read the audit trail (which the API does not expose).
    public static string ReviewDb => Environment.GetEnvironmentVariable("REVIEW_DB_CONNECTION")
        ?? throw new InvalidOperationException("Set REVIEW_DB_CONNECTION to the Review database connection string to run the ticket expiry tests.");

    public const string Password = "Str0ng!Pass1";
    public const string Question = "What is your favorite food?";
    public const string Answer = "pizza";

    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly Random Rng = new();
    private static readonly object Lock = new();

    public static string Letters(int length)
    {
        lock (Lock) return new string(Enumerable.Range(0, length).Select(_ => (char)('a' + Rng.Next(26))).ToArray());
    }

    public static string Digits(int length)
    {
        lock (Lock) return string.Concat(Enumerable.Range(0, length).Select(_ => Rng.Next(10)));
    }

    public static string Unique(string prefix) => prefix + Letters(10);

    // ---- raw HTTP -----------------------------------------------------------------------------------------

    public static async Task<Reply> Send(HttpMethod method, string url, object? body = null, string? token = null, string? internalKey = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (internalKey is not null) request.Headers.Add("X-Internal-Key", internalKey);
        using var response = await Http.SendAsync(request);
        return new Reply(response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public static Task<Reply> Get(string url, string? token = null) => Send(HttpMethod.Get, url, null, token);
    public static Task<Reply> Post(string url, object? body = null, string? token = null) => Send(HttpMethod.Post, url, body ?? new { }, token);

    // ---- accounts ----------------------------------------------------------------------------------------------

    public static async Task<AdminSession> NewAdmin(string? password = null)
    {
        var username = Unique("admin");
        var signup = await Post($"{IntakeUrl}/api/admins/signup", new
        {
            username, password = password ?? Password, passwordRecoveryQuestion = Question, passwordRecoveryAnswer = Answer,
            firstName = "Alice", lastName = Unique("Admin"), dateOfBirth = "1985-03-03", gender = "Female",
            ssn = Digits(9), isCitizen = true, countryOfCitizenship = "US",
        });
        signup.AssertOk("admin signup");
        return await LoginAdmin(username, password ?? Password);
    }

    public static async Task<AdminSession> LoginAdmin(string username, string password)
    {
        var login = await Post($"{IdentityUrl}/api/admins/login", new { username, password });
        login.AssertOk("admin login");
        var json = login.Json;
        return new AdminSession(username, json.GetProperty("token").GetString()!, json.GetProperty("adminId").GetInt32());
    }

    // ---- customers and tickets ------------------------------------------------------------------------------------

    // A customer entered through the admin's input form. Everything the test does not override trips no flag.
    public static async Task<Onboarded> Onboard(AdminSession admin, Action<CustomerForm>? change = null, int? expectStatus = null)
    {
        var form = new CustomerForm();
        change?.Invoke(form);
        var reply = await Post($"{IntakeUrl}/api/admins/inputForm", form.ToBody(), admin.Token);
        if (expectStatus is not null) Assert.Equal(expectStatus, (int)reply.Status);
        else reply.AssertOk("input form");
        return new Onboarded(form, reply.Status == HttpStatusCode.OK ? reply.Json.GetProperty("customerId").GetInt32() : 0,
            reply.Status == HttpStatusCode.OK ? reply.Json.GetProperty("ticketId").GetInt32() : 0);
    }

    public static async Task<Ticket> ReadTicket(AdminSession admin, int ticketId)
    {
        var reply = await Get($"{ReviewUrl}/api/admins/tickets/{ticketId}", admin.Token);
        reply.AssertOk($"read ticket {ticketId}");
        return new Ticket(reply.Json);
    }

    public static Task<Reply> Approve(AdminSession by, int ticketId, string reason = "All checks passed.") =>
        Post($"{ReviewUrl}/api/admins/tickets/{ticketId}/approve", new { reason }, by.Token);

    public static Task<Reply> Reject(AdminSession by, int ticketId, string reason = "Could not verify the customer.") =>
        Post($"{ReviewUrl}/api/admins/tickets/{ticketId}/reject", new { reason }, by.Token);

    public static Task<Reply> ClearFlag(AdminSession by, int ticketId, int flagId, string reason = "Checked in person.") =>
        Post($"{ReviewUrl}/api/admins/tickets/{ticketId}/flags/{flagId}/clear", new { reason }, by.Token);

    // Clears every open flag on the ticket, as a reviewer would.
    public static async Task ClearAllFlags(AdminSession by, int ticketId)
    {
        foreach (var flag in (await ReadTicket(by, ticketId)).Flags.Where(f => !f.Cleared))
            (await ClearFlag(by, ticketId, flag.FlagId)).AssertOk($"clear {flag.ViolationType}");
    }

    // ---- database ---------------------------------------------------------------------------------------------------

    public static async Task<T> Sql<T>(string sql, Func<SqlCommand, Task<T>> run, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(ReviewDb);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await run(command);
    }

    public static Task Exec(string sql, params (string, object)[] parameters) =>
        Sql<int>(sql, c => c.ExecuteNonQueryAsync(), parameters);

    public static Task<T?> Scalar<T>(string sql, params (string, object)[] parameters) =>
        Sql<T?>(sql, async c => await c.ExecuteScalarAsync() is { } v and not DBNull ? (T)v : default, parameters);

    // Moves a ticket's creation date (and so its 60-day deadline) into the past.
    public static Task Age(int ticketId, int days) => Exec(
        "UPDATE Tickets SET CreatedAt = DATEADD(DAY, -@d, CreatedAt), VerificationDeadline = DATEADD(DAY, -@d, VerificationDeadline) WHERE TicketId = @id",
        ("@d", days), ("@id", ticketId));

    public static Task RunExpiry() => Exec("EXEC dbo.pr_expireReviewTickets");
}

public record Reply(HttpStatusCode Status, string Body)
{
    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
    public int Code => (int)Status;

    public void AssertOk(string what) => Assert.True(Status == HttpStatusCode.OK, $"{what} failed: {Code} {Body}");
}

public record AdminSession(string Username, string Token, int AdminId);

public record Onboarded(CustomerForm Form, int CustomerId, int TicketId);

public record TicketFlag(int FlagId, string ViolationType, bool Cleared, string? ClearedBy, string? ClearedReason);

public class Ticket
{
    private readonly JsonElement _json;
    public Ticket(JsonElement json) => _json = json;

    public string Status => _json.GetProperty("ticketStatus").GetString()!;
    public string? RiskRating => _json.GetProperty("riskRating").GetString();
    public bool Verified => _json.GetProperty("verified").GetBoolean();
    public string? FirstApprovedBy => _json.GetProperty("firstApprovedBy").GetString();
    public string AssignedTo => _json.GetProperty("assignedTo").GetString()!;
    public DateTime Deadline => _json.GetProperty("verificationDeadline").GetDateTime();
    public DateTime CreatedAt => _json.GetProperty("createdAt").GetDateTime();

    public List<TicketFlag> Flags => _json.GetProperty("flags").EnumerateArray().Select(f => new TicketFlag(
        f.GetProperty("flagId").GetInt32(), f.GetProperty("violationType").GetString()!, f.GetProperty("cleared").GetBoolean(),
        f.GetProperty("clearedBy").GetString(), f.GetProperty("clearedReason").GetString())).ToList();

    public string[] FlagTypes => Flags.Select(f => f.ViolationType).OrderBy(x => x).ToArray();

    public JsonElement? Decision => _json.TryGetProperty("decision", out var d) && d.ValueKind == JsonValueKind.Object ? d : null;
    public string? DecisionType => Decision?.GetProperty("decisionType").GetString();
    public string? DecidedBy => Decision?.GetProperty("decidedBy").GetString();
    public string? DecisionReason => Decision?.GetProperty("reason").GetString();

    public JsonElement Vendor => _json.GetProperty("vendorResponse");
    public JsonElement Customer => _json.GetProperty("customer");
}

// The input form's fields. Defaults describe a customer that raises no flags.
public class CustomerForm
{
    public string FirstName = "Zed";
    public string MiddleName = "";
    public string LastName = Stack.Unique("Quill");
    public string TaxIdNumber = Stack.Digits(5) + "4321";
    public string Address = $"{Stack.Digits(3)} {Stack.Unique("Elm")} Street, Austin TX"; // unique, so a pending customer can be told apart from others with the same name
    public string Residence = "US";
    public string Citizenship = "US";
    public string Occupation = "Teacher";
    public string Employer = "Springfield School District";
    public string AccountType = "Individual";
    public int? JointCustomerId;
    public string Username = Stack.Unique("cust");
    public string Password = Stack.Password;

    public object ToBody() => new
    {
        firstName = FirstName, middleName = MiddleName, lastName = LastName, dateOfBirth = "1990-01-01", gender = "Male",
        taxIdType = "SSN", taxIdNumber = TaxIdNumber, residentialAddress = Address,
        countryOfResidence = Residence, countryOfCitizenship = Citizenship, occupation = Occupation, employer = Employer,
        typeOfAccount = AccountType, jointCustomerId = JointCustomerId,
        username = Username, password = Password, passwordRecoveryQuestion = Stack.Question, passwordRecoveryAnswer = Stack.Answer,
    };

    // The same customer entered again under a different login name.
    public object ToBodyWithNewUsername() { Username = Stack.Unique("cust"); return ToBody(); }

    // The four things Risk looks for, as form changes.
    public static readonly Dictionary<string, Action<CustomerForm>> Triggers = new()
    {
        ["VerificationFailed"] = f => f.TaxIdNumber = Stack.Digits(5) + "0000", // deceased last four digits
        ["OfacMatch"] = f => { f.FirstName = "James"; f.MiddleName = "Robert"; f.LastName = "Anderson"; },
        ["ClosedJurisdiction"] = f => f.Residence = "KP",
        ["ProhibitedIndustry"] = f => f.Occupation = "Cannabis dispensary owner",
    };

    public static Action<CustomerForm> Raising(params string[] flags) => f => { foreach (var flag in flags) Triggers[flag](f); };

    public static string RatingFor(string[] flags) =>
        flags.Contains("OfacMatch") || flags.Contains("ProhibitedIndustry") ? "High" : flags.Length > 0 ? "Medium" : "Low";
}

// The two reviewers every test can use, created once.
public static class Reviewers
{
    private static readonly Lazy<Task<(AdminSession One, AdminSession Two)>> Both = new(async () => (await Stack.NewAdmin(), await Stack.NewAdmin()));
    public static async Task<AdminSession> One() => (await Both.Value).One;
    public static async Task<AdminSession> Two() => (await Both.Value).Two;
}
