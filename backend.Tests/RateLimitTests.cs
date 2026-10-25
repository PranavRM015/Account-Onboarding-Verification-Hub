using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace backend.Tests
{
    public class RateLimitTests
    {
        private static WebApplicationFactory<Program> LimitedFactory(int permits)
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("TicketExpiry:Enabled", "false");
                b.UseSetting("RateLimiting:Auth:PermitLimit", permits.ToString());
                b.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
            });
        }

        [Fact]
        public async Task RepeatedLoginAttemptsAreBlockedWithTooManyRequests()
        {
            using var factory = LimitedFactory(3);
            var client = factory.CreateClient();
            var attempt = new { username = "nobody_" + Guid.NewGuid().ToString("N")[..8], password = HubFixture.Password };

            for (var i = 0; i < 3; i++)
            {
                var response = await client.PostAsJsonAsync("/api/admins/login", attempt);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }

            var blocked = await client.PostAsJsonAsync("/api/admins/login", attempt);
            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        }

        [Fact]
        public async Task SignupGuessesAreBlockedToo()
        {
            using var factory = LimitedFactory(2);
            var client = factory.CreateClient();
            var guess = new { username = "nobody_" + Guid.NewGuid().ToString("N")[..8], password = HubFixture.Password, signupCode = "guess" };

            for (var i = 0; i < 2; i++)
            {
                var response = await client.PostAsJsonAsync("/api/admins/signup", guess);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }

            var blocked = await client.PostAsJsonAsync("/api/admins/signup", guess);
            Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        }
    }
}
