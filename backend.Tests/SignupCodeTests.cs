using System.Net;
using System.Net.Http.Json;

namespace backend.Tests
{
    public class SignupCodeTests : IClassFixture<HubFixture>
    {
        private readonly HubFixture _hub;

        public SignupCodeTests(HubFixture hub)
        {
            _hub = hub;
        }

        private string NewName() => $"newadmin_{Guid.NewGuid():N}"[..20];

        [Fact]
        public async Task SignupWithTheCorrectCodeCreatesTheAdmin()
        {
            var username = NewName();
            _hub.TrackAdmin(username);
            var client = _hub.NewClient();

            var signup = await client.PostAsJsonAsync("/api/admins/signup",
                new { username, password = HubFixture.Password, signupCode = _hub.SignupCode });
            Assert.Equal(HttpStatusCode.OK, signup.StatusCode);

            var login = await client.PostAsJsonAsync("/api/admins/login", new { username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-the-code")]
        [InlineData("DEV-SIGNUP-CODE-2026")]
        public async Task SignupWithAWrongOrMissingCodeIsForbidden(string code)
        {
            var username = NewName();
            _hub.TrackAdmin(username);
            var client = _hub.NewClient();

            var signup = await client.PostAsJsonAsync("/api/admins/signup",
                new { username, password = HubFixture.Password, signupCode = code });
            Assert.Equal(HttpStatusCode.Forbidden, signup.StatusCode);

            var login = await client.PostAsJsonAsync("/api/admins/login", new { username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }

        [Fact]
        public async Task SignupWithNoCodeFieldAtAllIsForbidden()
        {
            var username = NewName();
            _hub.TrackAdmin(username);
            var signup = await _hub.NewClient().PostAsJsonAsync("/api/admins/signup",
                new { username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.Forbidden, signup.StatusCode);
        }
    }
}
