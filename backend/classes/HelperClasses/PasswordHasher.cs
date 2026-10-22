using System.Security.Cryptography;
using System.Text;
using BCrypt.Net;

namespace PasswordHasherNamespace
{
    public static class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32; // 256 bit
        private const int Iterations = 210000; // Number of iterations for PBKDF2
        private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;
        public static string PasswordHash(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, KeySize);
            return string.Join('.', Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }
        public static byte[] TaxIdHash(string taxIdNumber, string pepper)
        {
            var normalized = new string(taxIdNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            byte[] salt = Encoding.UTF8.GetBytes(pepper);
            return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(normalized), salt, Iterations, Algorithm, KeySize);
        }
        // PasswordHasher.Verify(password, storedHash)
        public static bool VerifyPassword(string password,string storedHash)
        {
            // Older rows may hold bcrypt hashes ($2a$, $2b$, ...)
            if (storedHash.StartsWith("$2"))
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHash);
            }
            // Current format written by PasswordHash: base64(salt).base64(hash)
            var parts = storedHash.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(parts[0]);
                byte[] expected = Convert.FromBase64String(parts[1]);
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
