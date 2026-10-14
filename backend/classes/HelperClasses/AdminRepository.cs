using Admins;
using Microsoft.Data.SqlClient;

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
            string hasher = PasswordHasher.PasswordHasher.PasswordHash(password);
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                var command = new SqlCommand("INSERT INTO Admins (Username, PasswordHash) VALUES (@Username, @PasswordHash)", connection);
                command.Parameters.AddWithValue("@Username", username);
                command.Parameters.AddWithValue("@PasswordHash", hasher);
                command.ExecuteNonQuery();
            }
        }

    }
}
