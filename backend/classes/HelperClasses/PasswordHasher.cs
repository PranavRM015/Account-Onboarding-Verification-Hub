using System.Security.Cryptography;
using System.Text;
namespace PasswordHasher
{
    public class PasswordHasher
    {
        private const int SaltSize = 16; // 128 bit
        private const int KeySize = 32; // 256 bit
        private const int Iterations = 210000; // Number of iterations for PBKDF2
        private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;
        public PasswordHasher() { }
        public static string PasswordHash(string p)
        {
            // Implementation for password hashing
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(p), salt, Iterations, HashAlgorithm, KeySize);
            return string.Join('.', Convert.ToBase64String(salt), Convert.ToBase64String(hash));
        }
    }
}