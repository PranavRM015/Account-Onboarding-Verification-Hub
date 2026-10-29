using System.Net;
using System.Net.Http.Json;

namespace backend.Tests
{
    public class LockoutTests : IClassFixture<HubFixture>
    {
        private readonly HubFixture _hub;

        public LockoutTests(HubFixture hub)
        {
            _hub = hub;
        }

        // Both roles follow the same rules, so each test runs once for admins and once for users.
        private async Task<string> NewAccount(string role) => role == "admins" ? await _hub.NewAdmin("lock") : await _hub.NewUser("lock");
        private string Table(string role) => role == "admins" ? "Admins" : "Customers";

        private Task<HttpResponseMessage> Login(string role, string username, string password) =>
            _hub.NewClient().PostAsJsonAsync($"/api/{role}/login", new { username, password });

        private async Task Fail(string role, string username, int times)
        {
            for (var i = 0; i < times; i++)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await Login(role, username, "Wrong!Passw0rd#1")).StatusCode);
            }
        }

        private void Unlock(string role, string username) =>
            _hub.Sql($"UPDATE {Table(role)} SET LockoutEnd = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE Username = @u", ("@u", username));

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TwoWrongPasswordsDoNotLockTheAccount(string role)
        {
            var username = await NewAccount(role);

            await Fail(role, username, 2);

            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TheThirdWrongPasswordLocksTheAccount(string role)
        {
            var username = await NewAccount(role);

            await Fail(role, username, 2);
            var third = await Login(role, username, "Wrong!Passw0rd#1");

            Assert.Equal(HttpStatusCode.Locked, third.StatusCode);
            Assert.Contains("15 more minutes", await third.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task ALockedAccountRejectsEvenTheCorrectPassword(string role)
        {
            var username = await NewAccount(role);
            await Fail(role, username, 2);
            await Login(role, username, "Wrong!Passw0rd#1");

            var response = await Login(role, username, HubFixture.Password);

            Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TheLockLastsFifteenMinutes(string role)
        {
            var username = await NewAccount(role);
            await Fail(role, username, 2);
            await Login(role, username, "Wrong!Passw0rd#1");

            var lockoutEnd = _hub.Scalar<DateTime>($"SELECT LockoutEnd FROM {Table(role)} WHERE Username = @u", ("@u", username));

            var minutes = (lockoutEnd - DateTime.UtcNow).TotalMinutes;
            Assert.InRange(minutes, 14, 15.1);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TheAccountWorksAgainOnceTheLockHasExpired(string role)
        {
            var username = await NewAccount(role);
            await Fail(role, username, 2);
            await Login(role, username, "Wrong!Passw0rd#1");
            Unlock(role, username);

            var response = await Login(role, username, HubFixture.Password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task AfterTheLockExpiresTheCountStartsAgain(string role)
        {
            var username = await NewAccount(role);
            await Fail(role, username, 2);
            await Login(role, username, "Wrong!Passw0rd#1");
            Unlock(role, username);

            await Fail(role, username, 2); // two more failures are not enough to lock again

            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task ASuccessfulLoginResetsTheFailureCount(string role)
        {
            var username = await NewAccount(role);
            await Fail(role, username, 2);
            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, HubFixture.Password)).StatusCode);

            await Fail(role, username, 2); // would be the 4th failure in a row if the count hadn't been reset

            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task LockingOneAccountDoesNotAffectAnother(string role)
        {
            var locked = await NewAccount(role);
            var other = await NewAccount(role);

            await Fail(role, locked, 2);
            await Login(role, locked, "Wrong!Passw0rd#1");

            Assert.Equal(HttpStatusCode.OK, (await Login(role, other, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task ALockedAccountCannotGetAToken(string role)
        {
            var username = await NewAccount(role);
            await Fail(role, username, 2);
            await Login(role, username, "Wrong!Passw0rd#1");

            var response = await Login(role, username, HubFixture.Password);

            Assert.DoesNotContain("token", await response.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task UnknownUsernamesJustGetTheGenericError(string role)
        {
            var response = await Login(role, $"nobody_{Guid.NewGuid():N}"[..20], "Wrong!Passw0rd#1");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AnAdminLockoutDoesNotLockAUserWithTheSameName()
        {
            var name = await _hub.NewAdmin("same");
            var signup = await _hub.NewClient().PostAsJsonAsync("/api/users/signup", _hub.UserBody(name));
            Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
            _hub.TrackCustomer(name);

            await Fail("admins", name, 2);
            await Login("admins", name, "Wrong!Passw0rd#1");

            Assert.Equal(HttpStatusCode.OK, (await Login("users", name, HubFixture.Password)).StatusCode);
        }
    }
}
