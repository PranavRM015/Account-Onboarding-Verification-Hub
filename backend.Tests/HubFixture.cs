using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketDtos;

namespace backend.Tests
{
    public class CustomerForm
    {
        public string FirstName { get; set; } = "Casey";
        public string MiddleName { get; set; } = "";
        public string LastName { get; set; } = "Testcase";
        public DateTime DateOfBirth { get; set; } = new DateTime(1985, 4, 12);
        public string Gender { get; set; } = "Other";
        public string TaxIdType { get; set; } = "SSN";
        public string TaxIdNumber { get; set; } = "";
        public string ResidentialAddress { get; set; } = "12 Main Street";
        public string CountryOfResidence { get; set; } = "US";
        public string CountryOfCitizenship { get; set; } = "US";
        public string Occupation { get; set; } = "Teacher";
        public string Employer { get; set; } = "City Public School";
        public string TypeOfAccount { get; set; } = "Individual";
    }

    public class HubFixture : IAsyncLifetime
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private static readonly Random Rng = new();
        private readonly List<int> _tickets = new();
        private readonly List<int> _customers = new();
        private readonly List<string> _admins = new();
        private WebApplicationFactory<Program> _factory = null!;
        private HashSet<string> _deceasedLastFour = new();

        public const string Password = "Str0ng!Passw0rd#2026";
        public string Suffix { get; } = Guid.NewGuid().ToString("N")[..8];
        public string ReviewerOne => $"reviewer_one_{Suffix}";
        public string ReviewerTwo => $"reviewer_two_{Suffix}";
        public HttpClient One { get; private set; } = null!;
        public HttpClient Two { get; private set; } = null!;
        public IConfiguration Configuration => _factory.Services.GetRequiredService<IConfiguration>();
        public IServiceProvider Services => _factory.Services;
        public string ConnectionString => Configuration.GetConnectionString("DefaultConnection")!;

        public async Task InitializeAsync()
        {
            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b => b.UseSetting("TicketExpiry:Enabled", "false"));
            var deceasedPath = Path.Combine(AppContext.BaseDirectory, "DeceasedSSNs.csv");
            _deceasedLastFour = File.ReadLines(deceasedPath).Skip(1).Select(l => l.Trim()).ToHashSet();
            One = await SignedInClient(ReviewerOne);
            Two = await SignedInClient(ReviewerTwo);
        }

        private async Task<HttpClient> SignedInClient(string username)
        {
            var client = _factory.CreateClient();
            var credentials = new { username, password = Password };
            var signup = await client.PostAsJsonAsync("/api/admins/signup", credentials);
            Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
            _admins.Add(username);
            var login = await client.PostAsJsonAsync("/api/admins/login", credentials);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            return client;
        }

        private string NewTaxId()
        {
            while (true)
            {
                var number = Rng.Next(100000000, 999999999).ToString();
                if (!_deceasedLastFour.Contains(number[^4..]))
                {
                    return number;
                }
            }
        }

        public async Task<TicketDetailsDto> Submit(CustomerForm form)
        {
            form.TaxIdNumber = NewTaxId();
            if (form.LastName == "Testcase")
            {
                form.LastName = "Testcase" + Suffix;
            }
            var response = await One.PostAsJsonAsync("/api/admins/inputForm", form);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var ticketId = body.RootElement.GetProperty("ticketId").GetInt32();
            _tickets.Add(ticketId);
            _customers.Add(body.RootElement.GetProperty("customerId").GetInt32());
            return await Read(ticketId);
        }

        public async Task<TicketDetailsDto> Read(int ticketId)
        {
            var response = await One.GetAsync($"/api/admins/tickets/{ticketId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<TicketDetailsDto>(Json))!;
        }

        public async Task ClearAllFlags(TicketDetailsDto ticket)
        {
            foreach (var flag in ticket.Flags)
            {
                var response = await One.PostAsJsonAsync(
                    $"/api/admins/tickets/{ticket.TicketId}/flags/{flag.FlagId}/clear",
                    new { reason = "Reviewed the customer's documents and cleared the flag." });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        public Task<HttpResponseMessage> Approve(HttpClient client, int ticketId, string reason = "All checks passed.")
        {
            return client.PostAsJsonAsync($"/api/admins/tickets/{ticketId}/approve", new { reason });
        }

        public Task<HttpResponseMessage> Reject(HttpClient client, int ticketId, string reason = "Could not verify the customer.")
        {
            return client.PostAsJsonAsync($"/api/admins/tickets/{ticketId}/reject", new { reason });
        }

        public void Backdate(int ticketId, int days)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = new SqlCommand(
                "DECLARE @c datetime2 = DATEADD(DAY, -@days, SYSUTCDATETIME()); " +
                "UPDATE Tickets SET CreatedAt = @c, VerificationDeadline = DATEADD(DAY, 60, @c) WHERE TicketId = @id",
                connection);
            command.Parameters.AddWithValue("@days", days);
            command.Parameters.AddWithValue("@id", ticketId);
            command.ExecuteNonQuery();
        }

        public int CountRows(string table, int ticketId)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE TicketId = @id", connection);
            command.Parameters.AddWithValue("@id", ticketId);
            return (int)command.ExecuteScalar()!;
        }

        public (string DecidedBy, string Reason)? SystemDecision(int ticketId)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT DecidedBy, Reason FROM Decisions WHERE TicketId = @id", connection);
            command.Parameters.AddWithValue("@id", ticketId);
            using var reader = command.ExecuteReader();
            return reader.Read() ? (reader.GetString(0), reader.GetString(1)) : null;
        }

        public string? AuditMessage(int ticketId, string currStatus)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = new SqlCommand(
                "SELECT TOP 1 ATMessage FROM AuditTrails WHERE TicketId = @id AND CurrStatus = @status ORDER BY RecId DESC",
                connection);
            command.Parameters.AddWithValue("@id", ticketId);
            command.Parameters.AddWithValue("@status", currStatus);
            return (string?)command.ExecuteScalar();
        }

        public async Task DisposeAsync()
        {
            One.Dispose();
            Two.Dispose();
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            foreach (var table in new[] { "Flags", "VendorResponses", "Decisions", "AuditTrails" })
            {
                foreach (var id in _tickets)
                {
                    Run(connection, $"DELETE FROM {table} WHERE TicketId = @id", ("@id", id));
                }
            }
            foreach (var id in _tickets)
            {
                Run(connection, "DELETE FROM Tickets WHERE TicketId = @id", ("@id", id));
            }
            foreach (var id in _customers)
            {
                Run(connection, "DELETE FROM Customers WHERE CustomerId = @id", ("@id", id));
            }
            foreach (var name in _admins)
            {
                Run(connection, "DELETE FROM Admins WHERE Username = @name", ("@name", name));
            }
            await _factory.DisposeAsync();
        }

        private static void Run(SqlConnection connection, string sql, (string Name, object Value) parameter)
        {
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            command.ExecuteNonQuery();
        }
    }
}
