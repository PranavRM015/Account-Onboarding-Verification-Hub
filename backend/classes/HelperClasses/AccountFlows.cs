using Data;
using Microsoft.AspNetCore.Mvc;
using PasswordHasherNamespace;

namespace AccountSecurity
{
    public class RecoveryAnswerRequest
    {
        public string username { get; set; } = string.Empty;
        public string answer { get; set; } = string.Empty;
        public string newPassword { get; set; } = string.Empty;
    }

    // The login and password-recovery steps are the same for admins and customers; only the table differs.
    public static class AccountFlows
    {
        // A throwaway hash, verified against when the username doesn't exist so that unknown names take as long to answer as real ones.
        private static readonly string DummyHash = PasswordHasher.PasswordHash("not-a-real-password");

        public static IActionResult Locked(ILoginAccount account, DateTime now) =>
            new ObjectResult(LoginLockout.Message(account, now)) { StatusCode = StatusCodes.Status423Locked };

        // Null when the password is right (the failure counter is cleared); otherwise the response to send back.
        public static async Task<IActionResult?> CheckPassword(AppDbContext db, ILoginAccount? account, string password)
        {
            var now = DateTime.UtcNow;
            if (account is null)
            {
                PasswordHasher.VerifyPassword(password, DummyHash);
                return new UnauthorizedObjectResult("Invalid username or password.");
            }
            if (LoginLockout.IsLocked(account, now))
            {
                return Locked(account, now);
            }
            if (PasswordHasher.VerifyPassword(password, account.PasswordHash))
            {
                if (account.FailedLoginCount != 0 || account.LockoutEnd is not null)
                {
                    LoginLockout.Reset(account);
                    await db.SaveChangesAsync();
                }
                return null;
            }
            var lockedNow = LoginLockout.RegisterFailure(account, now);
            await db.SaveChangesAsync();
            return lockedNow
                ? Locked(account, now)
                : new UnauthorizedObjectResult("Invalid username or password.");
        }

        public static IActionResult RecoveryQuestion(ILoginAccount? account, string username) =>
            new OkObjectResult(new { question = account?.PasswordRecoveryQuestion ?? RecoveryQuestions.Decoy(username) });

        public static async Task<IActionResult> ResetPassword(AppDbContext db, ILoginAccount? account, RecoveryAnswerRequest request)
        {
            var now = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(request.answer) || !PasswordPolicy.IsValid(request.newPassword))
            {
                return new BadRequestObjectResult($"An answer is required. {PasswordPolicy.Message}");
            }
            if (account is null)
            {
                RecoveryQuestions.Verify(request.answer, DummyHash);
                return new UnauthorizedObjectResult("Incorrect answer.");
            }
            // A locked account may still recover: the right answer clears the lock along with changing the password.
            // Wrong answers keep counting, so being locked doesn't give unlimited guesses.
            if (!RecoveryQuestions.Verify(request.answer, account.PasswordRecoveryAnswer))
            {
                LoginLockout.RegisterFailure(account, now);
                await db.SaveChangesAsync();
                return LoginLockout.IsLocked(account, now) ? Locked(account, now) : new UnauthorizedObjectResult("Incorrect answer.");
            }
            account.PasswordHash = PasswordHasher.PasswordHash(request.newPassword);
            LoginLockout.Reset(account);
            await db.SaveChangesAsync();
            return new OkObjectResult(new { message = "Password updated" });
        }
    }
}
