using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Tests
{
    public class LogoutTests : IClassFixture<HubFixture>
    {
        private readonly HubFixture _hub;

        public LogoutTests(HubFixture hub)
        {
            _hub = hub;
        }

        private async Task<string> NewAdminToken()
        {
            var username = $"logout_{Guid.NewGuid():N}"[..20];
            _hub.TrackAdmin(username);
            var client = _hub.NewClient();
            var signup = await client.PostAsJsonAsync("/api/admins/signup", new { username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
            var login = await client.PostAsJsonAsync("/api/admins/login", new { username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("token").GetString()!;
        }

        // A client that has no cookie, so only the bearer token can authenticate it.
        private HttpClient BearerClient(string token)
        {
            var client = _hub.NewClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        [Fact]
        public async Task LoginReturnsATokenThatAuthenticatesRequests()
        {
            var client = BearerClient(await NewAdminToken());

            var response = await client.GetAsync("/api/admins/inputForm/customers/1/exists");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task RequestsWithoutATokenAreUnauthorized()
        {
            var response = await _hub.NewClient().GetAsync("/api/admins/inputForm/customers/1/exists");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ATamperedTokenIsUnauthorized()
        {
            var client = BearerClient(await NewAdminToken() + "x");

            var response = await client.GetAsync("/api/admins/inputForm/customers/1/exists");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task LogoutRevokesTheTokenEvenThoughItHasNotExpired()
        {
            var token = await NewAdminToken();
            var client = BearerClient(token);

            var logout = await client.PostAsync("/api/admins/logout", null);
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

            var afterwards = await BearerClient(token).GetAsync("/api/admins/inputForm/customers/1/exists");
            Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
        }

        [Fact]
        public async Task LoggingOutOneTokenDoesNotRevokeAnother()
        {
            var first = await NewAdminToken();
            var second = await NewAdminToken();

            await BearerClient(first).PostAsync("/api/admins/logout", null);

            var response = await BearerClient(second).GetAsync("/api/admins/inputForm/customers/1/exists");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LogoutRequiresSignIn()
        {
            var response = await _hub.NewClient().PostAsync("/api/admins/logout", null);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task TheCookieStillWorksAsAFallbackAndLogoutClearsIt()
        {
            // _hub.One signed in through the login endpoint and holds the session cookie only.
            var client = _hub.NewClient();
            var username = $"cookie_{Guid.NewGuid():N}"[..20];
            _hub.TrackAdmin(username);
            await client.PostAsJsonAsync("/api/admins/signup", new { username, password = HubFixture.Password });
            await client.PostAsJsonAsync("/api/admins/login", new { username, password = HubFixture.Password });

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admins/inputForm/customers/1/exists")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/admins/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admins/inputForm/customers/1/exists")).StatusCode);
        }
    }
}
