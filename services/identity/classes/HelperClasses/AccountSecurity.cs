using System.Security.Cryptography;
using System.Text;
using PasswordHasherNamespace;

namespace AccountSecurity
{
    // What the lockout and password recovery logic needs from an Admin or a Customer.
    public interface ILoginAccount
    {
        string Username { get; }
        string PasswordHash { get; set; }
        string PasswordRecoveryQuestion { get; }
        string PasswordRecoveryAnswer { get; set; }
        int FailedLoginCount { get; set; }
        DateTime? LockoutEnd { get; set; }
    }

    public static class Roles
    {
        public const string Admin = "admin";
        public const string Customer = "customer";
    }

    // 3 wrong attempts lock the account for 15 minutes. Wrong recovery answers count too, so recovery can't be used to guess around the lock.
    public static class LoginLockout
    {
        public const int MaxAttempts = 3;
        public static readonly TimeSpan Duration = TimeSpan.FromMinutes(15);

        public static bool IsLocked(ILoginAccount account, DateTime now) =>
            account.LockoutEnd is { } end && end > now;

        public static int MinutesRemaining(ILoginAccount account, DateTime now) =>
            account.LockoutEnd is { } end ? Math.Max(1, (int)Math.Ceiling((end - now).TotalMinutes)) : 0;

        public static string Message(ILoginAccount account, DateTime now)
        {
            var minutes = MinutesRemaining(account, now);
            return $"Too many failed attempts. This account is locked for {minutes} more minute{(minutes == 1 ? "" : "s")}.";
        }

        // Returns true when this failure locked the account.
        public static bool RegisterFailure(ILoginAccount account, DateTime now)
        {
            if (account.LockoutEnd is { } end && end <= now)
            {
                account.LockoutEnd = null; // the old lock ran out, so start counting afresh
                account.FailedLoginCount = 0;
            }
            account.FailedLoginCount++;
            if (account.FailedLoginCount < MaxAttempts)
            {
                return false;
            }
            account.LockoutEnd = now.Add(Duration);
            account.FailedLoginCount = 0;
            return true;
        }

        public static void Reset(ILoginAccount account)
        {
            account.FailedLoginCount = 0;
            account.LockoutEnd = null;
        }
    }

    public static class RecoveryQuestions
    {
        public static readonly string[] All =
        {
            "What is your favorite food?",
            "What was the name of the elementary school you went to?",
            "What is the name of your first pet?",
            "Where was your last vacation?",
        };

        public static bool IsValid(string? question) => question is not null && All.Contains(question);

        // Unknown usernames get a question too, always the same one for the same name, so the screen doesn't reveal which accounts exist.
        public static string Decoy(string username)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(username.ToLowerInvariant()));
            return All[bytes[0] % All.Length];
        }

        // "Pizza " and "pizza" are the same answer.
        public static string Hash(string answer) => PasswordHasher.PasswordHash(Normalize(answer));
        public static bool Verify(string answer, string storedHash) => PasswordHasher.VerifyPassword(Normalize(answer), storedHash);
        private static string Normalize(string answer) => answer.Trim().ToLowerInvariant();
    }

    public static class PasswordPolicy
    {
        public const string Message = "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number and a special character.";

        public static bool IsValid(string? password) =>
            password is { Length: >= 8 }
            && password.Any(char.IsUpper) && password.Any(char.IsLower)
            && password.Any(char.IsDigit) && password.Any(c => !char.IsLetterOrDigit(c));
    }
}
