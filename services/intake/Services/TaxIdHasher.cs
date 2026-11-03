using System.Security.Cryptography;
using System.Text;

namespace Intake.Services;

// Hashes the tax ID number for duplicate detection. The algorithm must match
// the monolith's PasswordHasher.TaxIdHash() exactly so that hashes agree across services.
public static class TaxIdHasher
{
    private const int Iterations = 210_000;
    private const int KeySize    = 32;

    public static byte[] Hash(string taxIdNumber, string pepper)
    {
        var normalized = new string(
            taxIdNumber.Where(char.IsLetterOrDigit).ToArray()
        ).ToUpperInvariant();

        var salt      = Encoding.UTF8.GetBytes(pepper);
        var inputBytes = Encoding.UTF8.GetBytes(normalized);

        return Rfc2898DeriveBytes.Pbkdf2(
            inputBytes,
            salt,
            Iterations,
            HashAlgorithmName.SHA512,
            KeySize);
    }
}
