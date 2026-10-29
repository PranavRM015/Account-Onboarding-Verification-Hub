using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AccountSecurity;

namespace backend.Tests
{
    public class PasswordRecoveryTests : IClassFixture<HubFixture>
    {
        private const string NewPassword = "Brand#New9Passw0rd";
        private readonly HubFixture _hub;

        public PasswordRecoveryTests(HubFixture hub)
        {
            _hub = hub;
        }

        private async Task<string> NewAccount(string role) => role == "admins" ? await _hub.NewAdmin("rec") : await _hub.NewUser("rec");
        private string Table(string role) => role == "admins" ? "Admins" : "Customers";

        private Task<HttpResponseMessage> Login(string role, string username, string password) =>
            _hub.NewClient().PostAsJsonAsync($"/api/{role}/login", new { username, password });

        private Task<HttpResponseMessage> Reset(string role, string username, string answer, string newPassword = NewPassword) =>
            _hub.NewClient().PostAsJsonAsync($"/api/{role}/reset-password", new { username, answer, newPassword });

        private async Task<string> Question(string role, string username)
        {
            var response = await _hub.NewClient().GetAsync($"/api/{role}/recovery-question?username={username}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("question").GetString()!;
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TheQuestionIsTheOneChosenAtSignup(string role)
        {
            var username = await NewAccount(role);

            Assert.Equal(HubFixture.Question, await Question(role, username));
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task AnUnknownUsernameStillGetsAQuestionAndTheSameOneEachTime(string role)
        {
            var username = $"ghost_{Guid.NewGuid():N}"[..20];

            var first = await Question(role, username);
            var second = await Question(role, username);

            Assert.Equal(first, second);
            Assert.Contains(first, RecoveryQuestions.All);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TheRightAnswerSetsANewPassword(string role)
        {
            var username = await NewAccount(role);

            var reset = await Reset(role, username, HubFixture.Answer);

            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, NewPassword)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login(role, username, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TheAnswerIgnoresCaseAndSurroundingSpaces(string role)
        {
            var username = await NewAccount(role);

            var reset = await Reset(role, username, "  pIZZa ");

            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task AWrongAnswerChangesNothing(string role)
        {
            var username = await NewAccount(role);

            var reset = await Reset(role, username, "Sushi");

            Assert.Equal(HttpStatusCode.Unauthorized, reset.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task ThreeWrongAnswersLockTheAccount(string role)
        {
            var username = await NewAccount(role);

            await Reset(role, username, "Sushi");
            await Reset(role, username, "Tacos");
            var third = await Reset(role, username, "Curry");

            Assert.Equal(HttpStatusCode.Locked, third.StatusCode);
            // A normal login is refused while locked, and a wrong answer keeps the lock.
            Assert.Equal(HttpStatusCode.Locked, (await Login(role, username, HubFixture.Password)).StatusCode);
            Assert.Equal(HttpStatusCode.Locked, (await Reset(role, username, "Pasta")).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task TheRightAnswerOverridesALockoutAndSignsInWithTheNewPassword(string role)
        {
            var username = await NewAccount(role);
            await Reset(role, username, "Sushi");
            await Reset(role, username, "Tacos");
            Assert.Equal(HttpStatusCode.Locked, (await Reset(role, username, "Curry")).StatusCode);
            Assert.Equal(HttpStatusCode.Locked, (await Login(role, username, HubFixture.Password)).StatusCode);

            const string newPassword = "Brand!New#Passw0rd9";
            var reset = await Reset(role, username, HubFixture.Answer, newPassword);

            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
            Assert.Equal(1, _hub.Scalar<int>($"SELECT COUNT(*) FROM {Table(role)} WHERE Username = @u AND LockoutEnd IS NULL AND FailedLoginCount = 0", ("@u", username)));
            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, newPassword)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task ALockedAccountWithAWeakNewPasswordStaysLocked(string role)
        {
            var username = await NewAccount(role);
            await Reset(role, username, "Sushi");
            await Reset(role, username, "Tacos");
            await Reset(role, username, "Curry");

            var reset = await Reset(role, username, HubFixture.Answer, "weak");

            Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
            Assert.Equal(HttpStatusCode.Locked, (await Login(role, username, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task ARightAnswerClearsEarlierWrongOnes(string role)
        {
            var username = await NewAccount(role);
            await Reset(role, username, "Sushi");
            Assert.Equal(1, _hub.Scalar<int>($"SELECT FailedLoginCount FROM {Table(role)} WHERE Username = @u", ("@u", username)));

            await Reset(role, username, HubFixture.Answer);

            Assert.Equal(0, _hub.Scalar<int>($"SELECT FailedLoginCount FROM {Table(role)} WHERE Username = @u", ("@u", username)));
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task AWeakNewPasswordIsRejected(string role)
        {
            var username = await NewAccount(role);

            var reset = await Reset(role, username, HubFixture.Answer, "password");

            Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Login(role, username, HubFixture.Password)).StatusCode);
        }

        [Theory]
        [InlineData("admins")]
        [InlineData("users")]
        public async Task AnUnknownUsernameCannotBeReset(string role)
        {
            var reset = await Reset(role, $"ghost_{Guid.NewGuid():N}"[..20], "Pizza");

            Assert.Equal(HttpStatusCode.Unauthorized, reset.StatusCode);
        }

        [Fact]
        public async Task TheAnswerIsStoredHashedNotAsText()
        {
            var username = await _hub.NewUser("hash");

            var stored = _hub.Scalar<string>("SELECT PasswordRecoveryAnswer FROM Customers WHERE Username = @u", ("@u", username));

            Assert.NotEqual(HubFixture.Answer, stored);
            Assert.NotEqual(HubFixture.Answer.ToLowerInvariant(), stored);
            Assert.True(stored.Length <= 100);
        }
    }
}
