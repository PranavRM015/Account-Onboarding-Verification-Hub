using System.Net.Http.Json;
using Intake.DTOs;

namespace Intake.Services;

// Calls Review's internal ticket endpoints on behalf of Intake.
// HttpClient is registered in Program.cs with the base URL and a 15-second timeout.
// The admin's Bearer JWT is forwarded unchanged — Review validates it with Identity.
public class ReviewServiceClient
{
    private readonly HttpClient _http;

    public ReviewServiceClient(HttpClient http)
    {
        _http = http;
    }

    // Fetches the set of CustomerIds that already have a Review ticket (any status).
    // Returns the array on success, or null if Review is unreachable or returns an error.
    // null must NEVER be treated as an empty list — always return 503 to the caller on null.
    public async Task<int[]?> GetOnboardedCustomerIdsAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        try
        {
            var message = new HttpRequestMessage(
                HttpMethod.Get, "/internal/tickets/onboarded-customer-ids");
            message.Headers.Add("Authorization", $"Bearer {bearerToken}");
            var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<int[]>(ct);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Asks Review to create a ticket from the Risk assessment result.
    // bearerToken is the signed-in admin's JWT — forwarded unchanged.
    // Returns the raw HttpResponseMessage so the controller decides what to do with every status code.
    public async Task<HttpResponseMessage> CreateTicketAsync(
        ReviewCreateTicketDto request,
        string bearerToken,
        CancellationToken ct = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/internal/tickets");
        message.Headers.Add("Authorization", $"Bearer {bearerToken}");
        message.Content = JsonContent.Create(request);
        return await _http.SendAsync(message, ct);
    }

    // Checks whether Review already has a ticket for a given customer.
    // Used after an uncertain outcome (timeout/5xx) to decide whether a ticket was created.
    //
    // Returns one of three outcomes:
    //   Found    — Review returned 200; ticket exists and is populated.
    //   NotFound — Review returned 404; ticket definitely does not exist.
    //   Failed   — network error, timeout, 401/403/500, or any other unexpected response.
    //              Caller should not assume the ticket exists or does not exist.
    public async Task<TicketLookupResult> FindTicketByCustomerAsync(
        int customerId,
        string bearerToken,
        CancellationToken ct = default)
    {
        try
        {
            var message = new HttpRequestMessage(
                HttpMethod.Get,
                $"/internal/tickets/by-customer/{customerId}");
            message.Headers.Add("Authorization", $"Bearer {bearerToken}");
            var response = await _http.SendAsync(message, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return TicketLookupResult.NotFound();

            if (response.IsSuccessStatusCode)
            {
                var ticket = await response.Content.ReadFromJsonAsync<ReviewTicketResponseDto>(ct);
                return ticket is not null
                    ? TicketLookupResult.Found(ticket)
                    : TicketLookupResult.Failed();
            }

            // 401, 403, 500, 503, or anything else unexpected.
            return TicketLookupResult.Failed();
        }
        catch (Exception)
        {
            // Network error or timeout.
            return TicketLookupResult.Failed();
        }
    }
}

// Result type for FindTicketByCustomerAsync — three distinct outcomes.
public class TicketLookupResult
{
    public enum Outcome { Found, NotFound, Failed }

    public Outcome Status { get; private init; }
    public ReviewTicketResponseDto? Ticket { get; private init; }

    public bool IsFound    => Status == Outcome.Found;
    public bool IsNotFound => Status == Outcome.NotFound;
    public bool IsFailed   => Status == Outcome.Failed;

    public static TicketLookupResult Found(ReviewTicketResponseDto ticket) =>
        new() { Status = Outcome.Found, Ticket = ticket };

    public static TicketLookupResult NotFound() =>
        new() { Status = Outcome.NotFound };

    public static TicketLookupResult Failed() =>
        new() { Status = Outcome.Failed };
}
