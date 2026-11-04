using System.Net.Http.Json;

namespace Intake.Services;

// Calls the Identity service internal API on behalf of Intake.
// HttpClient is registered in Program.cs with the base URL and a 10-second timeout.
public class IdentityServiceClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public IdentityServiceClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _apiKey = configuration["Internal:ApiKey"]
            ?? throw new InvalidOperationException(
                "'Internal:ApiKey' is not configured. " +
                "Set the Internal__ApiKey environment variable.");
    }

    // Courtesy check before saving a profile. Returns true = available,
    // false = taken, null = Identity unreachable (proceed and handle 409 from Create).
    public async Task<bool?> IsUsernameAvailable(string role, string username, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/internal/accounts/username-available?role={Uri.EscapeDataString(role)}&username={Uri.EscapeDataString(username)}");
            request.Headers.Add("X-Internal-Key", _apiKey);

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadFromJsonAsync<UsernameAvailableResponse>(ct);
            return body?.available;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Creates the login in Identity. Returns the raw response for the controller to handle.
    public async Task<HttpResponseMessage> CreateAccount(
        string role,
        int id,
        string username,
        string password,
        string passwordRecoveryQuestion,
        string passwordRecoveryAnswer,
        CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/accounts");
        request.Headers.Add("X-Internal-Key", _apiKey);
        request.Content = JsonContent.Create(new
        {
            role,
            id,
            username,
            password,
            passwordRecoveryQuestion,
            passwordRecoveryAnswer,
        });

        return await _http.SendAsync(request, ct);
    }

    private sealed class UsernameAvailableResponse
    {
        public bool available { get; set; }
    }
}
