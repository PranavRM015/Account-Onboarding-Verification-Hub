using Microsoft.Data.SqlClient;
using PasswordHasherNamespace;
namespace AdminRepo
{
    public class AdminRepository
    {
        private readonly string _connectionString;

        public AdminRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public string? GetUsername(int AdminId)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("select Username from Admins where AdminId = @AdminId", connection);
            command.Parameters.AddWithValue("@AdminId", @AdminId);
            object? result = command.ExecuteScalar();
            return result?.ToString(); 
        }

        public void AddAdmin(string username, string password)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("INSERT INTO Admins (Username, PasswordHash) VALUES (@Username, @PasswordHash)", connection);
            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@PasswordHash", PasswordHasher.PasswordHash(password));
            var ticketId = command.ExecuteScalar();
            using var getAdminId = new SqlCommand("Select top 1 AdminId from Admins where Username = @Username", connection);
            getAdminId.Parameters.AddWithValue("@Username", username);
            object adminIdVal = getAdminId.ExecuteScalar();
            int AdminId = 0;
            if(adminIdVal!= null)
            {
                AdminId = Convert.ToInt32(adminIdVal);
            }
            
            //string userName = "";
            //userName = GetUsername(AdminId);
        }

        public int GetRecentTicket(int customerId)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT TOP 1 TicketId FROM Tickets WHERE CustomerId = @CustomerId ORDER BY CreatedAt DESC", connection);
            command.Parameters.AddWithValue("@CustomerId", customerId);
            var ticketId = command.ExecuteScalar();
            return ticketId is null ? 0 : (int)ticketId;
        }

        /// <summary>True when the admin exists and the password matches the stored salted hash.</summary>
        public bool ValidateAdmin(string username, string password)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT PasswordHash FROM Admins WHERE Username = @Username", connection);
            command.Parameters.AddWithValue("@Username", username);
            var storedHash = command.ExecuteScalar() as string;
            return storedHash != null && PasswordHasher.VerifyPassword(password, storedHash);
        }

        /// <summary>The username exactly as stored (SQL Server matches usernames case-insensitively), or null when there is no such admin.</summary>
        public string? GetStoredUsername(string username)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT Username FROM Admins WHERE Username = @Username", connection);
            command.Parameters.AddWithValue("@Username", username);
            return command.ExecuteScalar() as string;
        }

        public bool InsertTicket(int customerId, int? jointCustomerId, string accountType, string assignedTo)
        {
            var createdAt = DateTime.UtcNow;
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "INSERT INTO Tickets (CustomerId, JointCustomerId, TicketStatus, AccountType, CreatedAt, VerificationDeadline, AssignedTo) " +
                "VALUES (@CustomerId, @JointCustomerId, 'Submitted', @AccountType, @CreatedAt, @VerificationDeadline, @AssignedTo)", connection);
            command.Parameters.AddWithValue("@CustomerId", customerId);
            command.Parameters.AddWithValue("@TicketStatus", "Submitted");
            command.Parameters.AddWithValue("@AccountType", accountType);
            command.Parameters.AddWithValue("@CreatedAt", createdAt);
            command.Parameters.AddWithValue("@VerificationDeadline", createdAt.AddDays(60));
            command.Parameters.AddWithValue("@AssignedTo", assignedTo);
            command.Parameters.AddWithValue("@JointCustomerId", (object?)jointCustomerId ?? DBNull.Value);
            return command.ExecuteNonQuery() == 1;
        }

        public bool InsertAuditTrails(int ticketId, int customerId, string prevStatus, string currStatus, string ATMessage, string reason)
        {
            var dateTimeOccurred = DateTime.UtcNow;
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "INSERT INTO AuditTrails(TicketId, PrevStatus, CurrStatus, ATMessage, Reason, DateTimeOccurred, CustomerId) " +
                "VALUES (@ticketId, @prevStatus, @currStatus, @ATMessage, @reason, @dateTimeOccurred, @CustomerId)", connection);
            command.Parameters.AddWithValue("@ticketId",ticketId);
            command.Parameters.AddWithValue("@PrevStatus", prevStatus);
            command.Parameters.AddWithValue("@CurrStatus", currStatus);
            command.Parameters.AddWithValue("@ATMessage", ATMessage);
            command.Parameters.AddWithValue("@Reason", reason);
            command.Parameters.AddWithValue("@DateTimeOccurred", dateTimeOccurred);
            command.Parameters.AddWithValue("@CustomerId",customerId);
            return command.ExecuteNonQuery() == 1;
        }

        public bool InsertFlags(int ticketId, string violationType, int? raisedBy)
        {
            var raisedAt = DateTime.UtcNow;
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "INSERT INTO Flags(TicketId, ViolationType, RaisedBy, RaisedAt) " +
                "VALUES (@TicketId, @ViolationType, @RaisedBy, @RaisedAt)", connection);
            command.Parameters.AddWithValue("@TicketId", ticketId);
            command.Parameters.AddWithValue("@ViolationType", violationType);
            command.Parameters.AddWithValue("@RaisedBy", (object?)raisedBy ?? DBNull.Value);
            command.Parameters.AddWithValue("@RaisedAt", raisedAt);
            return command.ExecuteNonQuery() == 1;
        }

        public bool InsertDecision(int ticketId, string decisionType, string decidedBy, string reason)
        {
            var decidedAt = DateTime.UtcNow;
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "INSERT INTO Decisions(TicketId, DecisionType, DecidedBy, Reason, DecidedAt) " +
                "VALUES (@TicketId, @DecisionType, @DecidedBy, @Reason, @DecidedAt)", connection);
            command.Parameters.AddWithValue("@TicketId", ticketId);
            command.Parameters.AddWithValue("@DecisionType", decisionType);
            command.Parameters.AddWithValue("@DecidedBy", decidedBy);
            command.Parameters.AddWithValue("@Reason", reason);
            command.Parameters.AddWithValue("@DecidedAt", decidedAt);
            return command.ExecuteNonQuery() == 1;
        }

        public bool InsertVendorResponse(int ticketId, string nameMatch, string dateOfBirthMatch, string addressMatch, string taxIdMatch, int score, string riskCodes)
        {
            var VRTimeStamp = DateTime.UtcNow;
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "INSERT INTO VendorResponses(TicketId, NameMatch, DateOfBirthMatch, AddressMatch, TaxIdMatch, Score, RiskCodes, VRTimeStamp) " +
                "VALUES (@ticketId, @nameMatch, @dateOfBirthMatch, @addressMatch, @taxIdMatch, @score, @riskCodes, @VRTimeStamp)", connection);
            command.Parameters.AddWithValue("@TicketId", ticketId);
            command.Parameters.AddWithValue("@NameMatch", nameMatch);
            command.Parameters.AddWithValue("@DateOfBirthMatch", dateOfBirthMatch);
            command.Parameters.AddWithValue("@AddressMatch", addressMatch);
            command.Parameters.AddWithValue("@TaxIdMatch", taxIdMatch);
            command.Parameters.AddWithValue("@Score", score);
            command.Parameters.AddWithValue("@RiskCodes",riskCodes);
            command.Parameters.AddWithValue("@VRTimeStamp",VRTimeStamp);
            return command.ExecuteNonQuery() == 1;
        }

        public bool UpdateFlag(int clearedBy, int flagID, string reason)
        {
            DateTime clearedAt = DateTime.UtcNow;
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
            "UPDATE Flags SET clearedBy = @clearedBy, clearedAt = @clearedAt, ClearedReason = @reason WHERE flagID = @flagID AND clearedAt IS Null", connection);
            command.Parameters.AddWithValue("@clearedBy", clearedBy);
            command.Parameters.AddWithValue("@clearedAt", clearedAt);
            command.Parameters.AddWithValue("@flagID", flagID);
            command.Parameters.AddWithValue("@reason", reason);
            return command.ExecuteNonQuery() == 1;
        }

        public int[] GetFlagIDs(int ticketId)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
            "SELECT FlagId FROM Flags WHERE TicketId = @TicketId", connection);
            command.Parameters.AddWithValue("@TicketId", ticketId);
            var flagIds = new List<int>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                flagIds.Add(reader.GetInt32(0));
            }
            return flagIds.ToArray();
        }

        public bool CheckRemainingFlagCount(int ticketId)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
            "SELECT COUNT(*) FROM Flags WHERE TicketId = @TicketId AND ClearedAt IS NULL", connection);
            command.Parameters.AddWithValue("@TicketId", ticketId);
            return (int)command.ExecuteScalar()! == 0;
        }

        public bool AssignRiskRating(int ticketId, string riskRating)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                "UPDATE Tickets SET riskRating = @riskRating WHERE ticketId = @ticketId", connection);
            command.Parameters.AddWithValue("@riskRating", riskRating);
            command.Parameters.AddWithValue("@ticketId", ticketId);
            return command.ExecuteNonQuery() == 1;
        }

        /// <summary>True when a customer with this id exists.</summary>
        public bool CustomerExists(int customerId)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT COUNT(1) FROM Customers WHERE CustomerId = @CustomerId", connection);
            command.Parameters.AddWithValue("@CustomerId", customerId);
            return (int)command.ExecuteScalar()! > 0;
        }
    }
}
