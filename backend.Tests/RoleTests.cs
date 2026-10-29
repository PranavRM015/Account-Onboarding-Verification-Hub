using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace backend.Tests
{
    public class RoleTests : IClassFixture<HubFixture>
    {
        private const string AdminEndpoint = "/api/admins/inputForm/customers/1/exists";
        private readonly HubFixture _hub;

        public RoleTests(HubFixture hub)
        {
            _hub = hub;
        }

        private async Task<HttpClient> BearerFor(string role, string username)
        {
            var login = await _hub.NewClient().PostAsJsonAsync($"/api/{role}/login", new { username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            var client = _hub.NewClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("token").GetString());
            return client;
        }

        [Fact]
        public async Task AnAdminTokenReachesAdminEndpoints()
        {
            var client = await BearerFor("admins", await _hub.NewAdmin("role"));

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AdminEndpoint)).StatusCode);
        }

        [Fact]
        public async Task AUserTokenIsForbiddenFromAdminEndpoints()
        {
            var client = await BearerFor("users", await _hub.NewUser("role"));

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(AdminEndpoint)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admins/tickets/1")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admins/inputForm", new { })).StatusCode);
        }

        [Fact]
        public async Task AUserCookieIsForbiddenFromAdminEndpointsToo()
        {
            var username = await _hub.NewUser("cookie");
            var client = _hub.NewClient(); // keeps the session cookie from the login
            await client.PostAsJsonAsync("/api/users/login", new { username, password = HubFixture.Password });

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(AdminEndpoint)).StatusCode);
        }

        [Fact]
        public async Task AnAdminTokenIsForbiddenFromUserEndpoints()
        {
            var client = await BearerFor("admins", await _hub.NewAdmin("role"));

            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users/me")).StatusCode);
        }

        [Fact]
        public async Task AUserTokenReachesUserEndpoints()
        {
            var username = await _hub.NewUser("role");
            var client = await BearerFor("users", username);

            var response = await client.GetAsync("/api/users/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(username, await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task UserCredentialsCannotLogInAsAnAdmin()
        {
            var username = await _hub.NewUser("onlyuser");

            var response = await _hub.NewClient().PostAsJsonAsync("/api/admins/login", new { username, password = HubFixture.Password });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AdminCredentialsCannotLogInAsAUser()
        {
            var username = await _hub.NewAdmin("onlyadm");

            var response = await _hub.NewClient().PostAsJsonAsync("/api/users/login", new { username, password = HubFixture.Password });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task TheLoginResponseNamesTheRole()
        {
            var admin = await _hub.NewAdmin("named");
            var user = await _hub.NewUser("named");

            var adminLogin = await _hub.NewClient().PostAsJsonAsync("/api/admins/login", new { username = admin, password = HubFixture.Password });
            var userLogin = await _hub.NewClient().PostAsJsonAsync("/api/users/login", new { username = user, password = HubFixture.Password });

            Assert.Contains("\"role\":\"Admin\"", await adminLogin.Content.ReadAsStringAsync());
            Assert.Contains("\"role\":\"User\"", await userLogin.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task AUserCanLogOutAndTheTokenStopsWorking()
        {
            var client = await BearerFor("users", await _hub.NewUser("bye"));

            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/admins/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
        }
    }
}
