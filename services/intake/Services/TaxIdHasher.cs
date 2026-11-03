using System.Security.Cryptography;
using System.Text;

namespace Intake.Services;

// Computes the TaxId hash that is stored in the Customers table.
// This is a profile-integrity operation: it lets Intake detect duplicate
// customers without ever storing the raw tax ID number.
//
// The algorithm must match the monolith exactly so that hashes produced
// here are identical to hashes produced by the Identity service.
//   Algorithm : PBKDF2-SHA512
//   Iterations: 210,000
//   Key size  : 32 bytes (256 bit)
//   Salt      : UTF-8 bytes of the TaxIdHashPepper configuration value
//
// The pepper is a server secret — supply it through the TaxIdHashPepper
// environment variable. Never hardcode it here.
public static class TaxIdHasher
{
    private const int Iterations = 210_000;
    private const int KeySize    = 32;

    public static byte[] Hash(string taxIdNumber, string pepper)
    {
        // Strip everything except alphanumeric characters and normalise to
        // upper-case, matching the monolith's PasswordHasher.TaxIdHash().
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
