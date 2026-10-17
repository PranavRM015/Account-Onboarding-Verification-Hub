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

        public void AddAdmin(string username, string password)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("INSERT INTO Admins (Username, PasswordHash) VALUES (@Username, @PasswordHash)", connection);
            command.Parameters.AddWithValue("@Username", username);
            command.Parameters.AddWithValue("@PasswordHash", PasswordHasher.PasswordHash(password));
            command.ExecuteNonQuery();
        }

        /// <summary>True when the admin exists and the password matches the stored salted hash.</summary>
        public bool ValidateAdmin(string username, string password)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT PasswordHash FROM Admins WHERE Username = @Username", connection);
            command.Parameters.AddWithValue("@Username", username);
            var storedHash = command.ExecuteScalar() as string;
            return storedHash != null && PasswordHasher.Verify(password, storedHash);
        }
    }
}
