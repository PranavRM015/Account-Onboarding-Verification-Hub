using System.Net;
using System.Net.Http.Json;

namespace backend.Tests
{
    public class SignupTests : IClassFixture<HubFixture>
    {
        private readonly HubFixture _hub;

        public SignupTests(HubFixture hub)
        {
            _hub = hub;
        }

        private string NewName() => $"newadmin_{Guid.NewGuid():N}"[..20];

        [Fact]
        public async Task SignupCreatesTheAdminWhoCanThenLogIn()
        {
            var username = NewName();
            _hub.TrackAdmin(username);
            var client = _hub.NewClient();

            var signup = await client.PostAsJsonAsync("/api/admins/signup", _hub.AdminBody(username));
            Assert.Equal(HttpStatusCode.OK, signup.StatusCode);

            var login = await client.PostAsJsonAsync("/api/admins/login", new { username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        [Fact]
        public async Task SignupWithATakenUsernameIsAConflict()
        {
            var username = NewName();
            _hub.TrackAdmin(username);
            var client = _hub.NewClient();

            var first = await client.PostAsJsonAsync("/api/admins/signup", _hub.AdminBody(username));
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await client.PostAsJsonAsync("/api/admins/signup", _hub.AdminBody(username));
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
    }
}
