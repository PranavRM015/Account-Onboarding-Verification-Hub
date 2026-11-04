using System.Net.Http.Json;
using Intake.DTOs;

namespace Intake.Services;

// Calls Risk's POST /internal/analyze endpoint.
// HttpClient is registered in Program.cs with the base URL and a 15-second timeout.
// The admin's Bearer JWT is forwarded unchanged — Risk validates it directly with Identity.
public class RiskServiceClient
{
    private readonly HttpClient _http;

    public RiskServiceClient(HttpClient http)
    {
        _http = http;
    }

    // Sends the customer profile to Risk for screening.
    // The bearerToken is the signed-in admin's JWT from the incoming request —
    // forward it in the Authorization header exactly as received.
    // Returns the raw HttpResponseMessage so the controller can inspect the status and body.
    public async Task<HttpResponseMessage> AnalyzeAsync(
        RiskAnalyzeRequest request,
        string bearerToken,
        CancellationToken ct = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/internal/analyze");
        message.Headers.Add("Authorization", $"Bearer {bearerToken}");
        message.Content = JsonContent.Create(request);
        return await _http.SendAsync(message, ct);
    }
}
