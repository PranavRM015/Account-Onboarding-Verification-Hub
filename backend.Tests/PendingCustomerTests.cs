using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Tests
{
    // Customers who signed themselves up have no ticket yet. Admins see them in a list, oldest first, and open one to
    // see exactly what the customer entered. The database is shared, so each test looks only at its own customers.
    public class PendingCustomerTests : IClassFixture<HubFixture>
    {
        private const string PendingUrl = "/api/admins/pending-customers";
        private readonly HubFixture _hub;

        public PendingCustomerTests(HubFixture hub)
        {
            _hub = hub;
        }

        private sealed record Pending(int CustomerId, string FirstName, string? MiddleName, string LastName, DateTimeOffset CreatedAt);

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        // The waiting customers among the ones this test made, in the order the admin sees them.
        private async Task<List<Pending>> Waiting(params int[] mine)
        {
            var response = await _hub.One.GetAsync(PendingUrl);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var all = await response.Content.ReadFromJsonAsync<List<Pending>>(Json);
            return all!.Where(p => mine.Contains(p.CustomerId)).ToList();
        }

        private void MadeHoursAgo(int customerId, int hours) =>
            _hub.Sql("UPDATE Customers SET CreatedAt = DATEADD(HOUR, -@h, SYSUTCDATETIME()) WHERE CustomerId = @id", ("@h", hours), ("@id", customerId));

        private async Task<int> Open(int customerId, string type = "Individual", int? jointCustomerId = null)
        {
            var response = await _hub.One.PostAsJsonAsync($"{PendingUrl}/{customerId}/submit", new { typeOfAccount = type, jointCustomerId });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var ticketId = body.RootElement.GetProperty("ticketId").GetInt32();
            _hub.TrackTicket(ticketId);
            return ticketId;
        }

        private async Task<HttpStatusCode> UserLogin(string username, string password) =>
            (await _hub.NewClient().PostAsJsonAsync("/api/users/login", new { username, password })).StatusCode;

        [Fact]
        public async Task OneUserWhoSignedUpAppearsWithTheirNameAndWhenTheySignedUp()
        {
            var before = DateTimeOffset.UtcNow;
            var (_, id) = await _hub.NewUser("one", "First!Passw0rd#1", "Dana", "Firstfilled" + _hub.Suffix);

            var waiting = await Waiting(id);

            var entry = Assert.Single(waiting);
            Assert.Equal("Dana", entry.FirstName);
            Assert.Equal("Firstfilled" + _hub.Suffix, entry.LastName);
            Assert.InRange(entry.CreatedAt, before.AddMinutes(-2), DateTimeOffset.UtcNow.AddMinutes(2));
            Assert.Equal(TimeSpan.Zero, entry.CreatedAt.Offset); // sent as UTC, so the browser shows it in the viewer's own time zone
        }

        [Fact]
        public async Task OpeningACustomerShowsOnlyWhatTheyEntered()
        {
            var username = $"det_{Guid.NewGuid():N}"[..20];
            _hub.TrackCustomer(username);
            var taxId = _hub.NewTaxId();
            var signup = await _hub.NewClient().PostAsJsonAsync("/api/users/signup", new
            {
                username, password = "Detail!Passw0rd#1", firstName = "Robin", middleName = "Lee", lastName = "Detailed",
                dateOfBirth = new DateTime(1979, 8, 3), gender = "Female", taxIdType = "ITIN", taxIdNumber = taxId,
                residentialAddress = "9 Elm Street, Albany, NY, 12207", countryOfResidence = "CA", countryOfCitizenship = "MX",
                occupation = "Nurse", employer = "General Hospital", passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = "Tacos",
            });
            Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
            var id = _hub.Scalar<int>("SELECT CustomerId FROM Customers WHERE Username = @u", ("@u", username));

            var response = await _hub.One.GetAsync($"{PendingUrl}/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;
            Assert.Equal("Robin", root.GetProperty("firstName").GetString());
            Assert.Equal("Lee", root.GetProperty("middleName").GetString());
            Assert.Equal("Detailed", root.GetProperty("lastName").GetString());
            Assert.Equal("1979-08-03", root.GetProperty("dateOfBirth").GetString());
            Assert.Equal("Female", root.GetProperty("gender").GetString());
            Assert.Equal("ITIN", root.GetProperty("taxIdType").GetString());
            Assert.Equal(taxId[^4..], root.GetProperty("taxIdLastFourDigits").GetString());
            Assert.Equal("9 Elm Street, Albany, NY, 12207", root.GetProperty("residentialAddress").GetString());
            Assert.Equal("CA", root.GetProperty("countryOfResidence").GetString());
            Assert.Equal("MX", root.GetProperty("countryOfCitizenship").GetString());
            Assert.Equal("Nurse", root.GetProperty("occupation").GetString());
            Assert.Equal("General Hospital", root.GetProperty("employer").GetString());

            // Nothing the customer didn't give is sent: no account type, and no secrets or full tax ID.
            var names = root.EnumerateObject().Select(p => p.Name).ToHashSet();
            foreach (var absent in new[] { "typeOfAccount", "jointCustomerId", "username", "password", "passwordHash", "taxIdNumber", "taxIdHash", "passwordRecoveryAnswer" })
            {
                Assert.DoesNotContain(absent, names);
            }
            Assert.DoesNotContain(taxId, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task OpeningTheTicketTakesTheCustomerOffTheListAndKeepsTheirPassword()
        {
            const string password = "Keepme!Passw0rd#1";
            var (username, id) = await _hub.NewUser("keep", password, "Kim", "Keeper" + _hub.Suffix);

            var ticketId = await Open(id);

            Assert.Empty(await Waiting(id));
            Assert.Equal(_hub.ReviewerOne, _hub.Scalar<string>("SELECT AssignedTo FROM Tickets WHERE TicketId = @t", ("@t", ticketId)));
            Assert.Equal(HttpStatusCode.OK, await UserLogin(username, password));
            Assert.Equal(HttpStatusCode.Conflict, (await _hub.One.PostAsJsonAsync($"{PendingUrl}/{id}/submit", new { typeOfAccount = "Individual" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await _hub.One.GetAsync($"{PendingUrl}/{id}")).StatusCode);
        }

        [Fact]
        public async Task ACustomerTheAdminEnteredThemselvesIsNeverWaiting()
        {
            var form = new CustomerForm();
            await _hub.Submit(form);
            var id = _hub.Scalar<int>("SELECT CustomerId FROM Customers WHERE Username = @u", ("@u", form.Username));

            Assert.Empty(await Waiting(id));
        }

        [Fact]
        public async Task AJointAccountNeedsAnExistingSecondHolder()
        {
            var (_, first) = await _hub.NewUser("jnt", HubFixture.Password, "Joan", "Joint" + _hub.Suffix);
            var (_, second) = await _hub.NewUser("jnt", HubFixture.Password, "Jules", "Joint" + _hub.Suffix);
            Task<HttpResponseMessage> Try(object body) => _hub.One.PostAsJsonAsync($"{PendingUrl}/{first}/submit", body);

            Assert.Equal(HttpStatusCode.BadRequest, (await Try(new { typeOfAccount = "Joint" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Try(new { typeOfAccount = "Joint", jointCustomerId = int.MaxValue })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Try(new { typeOfAccount = "Joint", jointCustomerId = first })).StatusCode);
            Assert.Equal(2, (await Waiting(first, second)).Count); // none of the failed tries opened a ticket

            await Open(first, "Joint", second);

            Assert.Equal(second, Assert.Single(await Waiting(first, second)).CustomerId); // the second holder still has to be onboarded on their own
        }

        [Fact]
        public async Task AnUnknownAccountTypeIsRejectedAndTheCustomerKeepsWaiting()
        {
            var (_, id) = await _hub.NewUser("typ", HubFixture.Password, "Tess", "Typed" + _hub.Suffix);

            Assert.Equal(HttpStatusCode.BadRequest, (await _hub.One.PostAsJsonAsync($"{PendingUrl}/{id}/submit", new { typeOfAccount = "Business" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await _hub.One.PostAsJsonAsync($"{PendingUrl}/{id}/submit", new { typeOfAccount = "" })).StatusCode);

            Assert.Single(await Waiting(id));
        }

        [Fact]
        public async Task AnUnknownCustomerIsNotFound()
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _hub.One.GetAsync($"{PendingUrl}/{int.MaxValue}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await _hub.One.PostAsJsonAsync($"{PendingUrl}/{int.MaxValue}/submit", new { typeOfAccount = "Individual" })).StatusCode);
        }

        [Fact]
        public async Task SeveralUsersWhoSignedUpAtDifferentTimesAreListedOldestFirstEachWithTheirOwnPassword()
        {
            // Signed up in the order C, A, B, then dated so that A was first, B second and C third. The list follows the dates, not the IDs.
            var c = await _hub.NewUser("many", "Third!Passw0rd#3", "Cleo", "Third" + _hub.Suffix);
            var a = await _hub.NewUser("many", "First!Passw0rd#1", "Alma", "First" + _hub.Suffix);
            var b = await _hub.NewUser("many", "Second!Passw0rd#2", "Bryn", "Second" + _hub.Suffix);
            MadeHoursAgo(a.CustomerId, 3);
            MadeHoursAgo(b.CustomerId, 2);
            MadeHoursAgo(c.CustomerId, 1);

            var waiting = await Waiting(a.CustomerId, b.CustomerId, c.CustomerId);

            Assert.Equal(new[] { a.CustomerId, b.CustomerId, c.CustomerId }, waiting.Select(w => w.CustomerId));
            Assert.Equal(new[] { "Alma", "Bryn", "Cleo" }, waiting.Select(w => w.FirstName));
            Assert.True(waiting[0].CreatedAt < waiting[1].CreatedAt && waiting[1].CreatedAt < waiting[2].CreatedAt);
            Assert.InRange((DateTimeOffset.UtcNow - waiting[0].CreatedAt).TotalMinutes, 179, 181);

            // Each person's own password works, and a different person's password doesn't.
            Assert.Equal(HttpStatusCode.OK, await UserLogin(a.Username, "First!Passw0rd#1"));
            Assert.Equal(HttpStatusCode.OK, await UserLogin(b.Username, "Second!Passw0rd#2"));
            Assert.Equal(HttpStatusCode.OK, await UserLogin(c.Username, "Third!Passw0rd#3"));
            Assert.Equal(HttpStatusCode.Unauthorized, await UserLogin(a.Username, "Second!Passw0rd#2"));

            // Onboarding the middle one leaves the other two waiting, still in order, and nobody's password changes.
            await Open(b.CustomerId);

            var remaining = await Waiting(a.CustomerId, b.CustomerId, c.CustomerId);
            Assert.Equal(new[] { a.CustomerId, c.CustomerId }, remaining.Select(w => w.CustomerId));
            Assert.Equal(HttpStatusCode.OK, await UserLogin(a.Username, "First!Passw0rd#1"));
            Assert.Equal(HttpStatusCode.OK, await UserLogin(b.Username, "Second!Passw0rd#2"));
            Assert.Equal(HttpStatusCode.OK, await UserLogin(c.Username, "Third!Passw0rd#3"));
        }

        [Fact]
        public async Task EveryAdminSeesTheSameWaitingCustomers()
        {
            var (_, id) = await _hub.NewUser("shr", HubFixture.Password, "Sam", "Shared" + _hub.Suffix);

            var response = await _hub.Two.GetAsync(PendingUrl);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var seenByTwo = await response.Content.ReadFromJsonAsync<List<Pending>>(Json);
            Assert.Contains(seenByTwo!, p => p.CustomerId == id);
            Assert.Single(await Waiting(id));
        }

        [Fact]
        public async Task OnlyAdminsCanSeeOrOpenWaitingCustomers()
        {
            var (username, id) = await _hub.NewUser("sec", HubFixture.Password, "Sky", "Secure" + _hub.Suffix);
            var anonymous = _hub.NewClient();
            var login = await _hub.NewClient().PostAsJsonAsync("/api/users/login", new { username, password = HubFixture.Password });
            using var token = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            var user = _hub.NewClient();
            user.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.RootElement.GetProperty("token").GetString());

            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(PendingUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(PendingUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"{PendingUrl}/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync($"{PendingUrl}/{id}/submit", new { typeOfAccount = "Individual" })).StatusCode);
            Assert.Single(await Waiting(id)); // and the customer is still waiting
        }
    }
}
