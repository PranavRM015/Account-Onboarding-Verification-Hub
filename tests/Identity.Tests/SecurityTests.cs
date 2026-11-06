using AccountSecurity;
using Accounts;
using Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordHasherNamespace;

namespace Identity.Tests;

internal static class Db
{
    public static IdentityDbContext New() => new(new DbContextOptionsBuilder<IdentityDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public static AdminAccount Admin(string username = "alice", string password = "Str0ng!Pass", string answer = "pizza") => new()
    {
        AdminId = 1, Username = username,
        PasswordHash = PasswordHasher.PasswordHash(password),
        PasswordRecoveryQuestion = RecoveryQuestions.All[0],
        PasswordRecoveryAnswer = RecoveryQuestions.Hash(answer),
    };
}

public class PasswordHasherTests
{
    [Fact]
    public void HashedPassword_VerifiesOnlyWithTheSamePassword()
    {
        var hash = PasswordHasher.PasswordHash("Str0ng!Pass");

        Assert.True(PasswordHasher.VerifyPassword("Str0ng!Pass", hash));
        Assert.False(PasswordHasher.VerifyPassword("str0ng!pass", hash));
        Assert.False(PasswordHasher.VerifyPassword("", hash));
    }

    [Fact]
    public void SamePassword_GetsADifferentSaltEachTime()
    {
        Assert.NotEqual(PasswordHasher.PasswordHash("Str0ng!Pass"), PasswordHasher.PasswordHash("Str0ng!Pass"));
    }

    [Fact]
    public void LegacyBcryptHashes_StillVerify()
    {
        var bcrypt = BCrypt.Net.BCrypt.HashPassword("Legacy!1Pass", workFactor: 4);

        Assert.True(PasswordHasher.VerifyPassword("Legacy!1Pass", bcrypt));
        Assert.False(PasswordHasher.VerifyPassword("wrong", bcrypt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")] // right shape, not base64
    public void MalformedStoredHashes_NeverVerify(string stored) => Assert.False(PasswordHasher.VerifyPassword("anything", stored));
}

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Str0ng!Pass")]
    [InlineData("Aa1!aaaa")]
    public void StrongPasswords_Pass(string password) => Assert.True(PasswordPolicy.IsValid(password));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Aa1!aaa")]        // 7 characters
    [InlineData("alllowercase1!")] // no uppercase
    [InlineData("ALLUPPERCASE1!")] // no lowercase
    [InlineData("NoDigits!Here")]
    [InlineData("NoSpecial1Here")]
    public void WeakPasswords_Fail(string? password) => Assert.False(PasswordPolicy.IsValid(password));
}

public class LoginLockoutTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ThirdFailure_LocksTheAccountFor15Minutes()
    {
        var account = Db.Admin();

        Assert.False(LoginLockout.RegisterFailure(account, Now));
        Assert.False(LoginLockout.RegisterFailure(account, Now));
        Assert.True(LoginLockout.RegisterFailure(account, Now));

        Assert.Equal(Now.AddMinutes(15), account.LockoutEnd);
        Assert.True(LoginLockout.IsLocked(account, Now.AddMinutes(14)));
        Assert.False(LoginLockout.IsLocked(account, Now.AddMinutes(15)));
        Assert.Equal(0, account.FailedLoginCount);
    }

    [Fact]
    public void FailureAfterTheLockExpired_StartsCountingAgain()
    {
        var account = Db.Admin();
        account.LockoutEnd = Now.AddMinutes(-1);
        account.FailedLoginCount = 0;

        Assert.False(LoginLockout.RegisterFailure(account, Now));

        Assert.Null(account.LockoutEnd);
        Assert.Equal(1, account.FailedLoginCount);
    }

    [Fact]
    public void Reset_ClearsTheCounterAndTheLock()
    {
        var account = Db.Admin();
        account.FailedLoginCount = 2; account.LockoutEnd = Now.AddMinutes(5);

        LoginLockout.Reset(account);

        Assert.Equal(0, account.FailedLoginCount);
        Assert.Null(account.LockoutEnd);
    }

    [Theory]
    [InlineData(1, "1 more minute.")]
    [InlineData(2, "2 more minutes.")]
    [InlineData(15, "15 more minutes.")]
    public void Message_SaysHowLongIsLeft(int minutes, string expectedEnd)
    {
        var account = Db.Admin();
        account.LockoutEnd = Now.AddMinutes(minutes);

        Assert.EndsWith(expectedEnd, LoginLockout.Message(account, Now));
    }

    [Fact]
    public void MinutesRemaining_IsAtLeastOne_WhileLocked_AndZeroWhenNeverLocked()
    {
        var account = Db.Admin();
        Assert.Equal(0, LoginLockout.MinutesRemaining(account, Now));
        account.LockoutEnd = Now.AddSeconds(5);
        Assert.Equal(1, LoginLockout.MinutesRemaining(account, Now));
    }
}

public class RecoveryQuestionTests
{
    [Fact] public void ThereAreFourQuestions_AllValid() { Assert.Equal(4, RecoveryQuestions.All.Length); Assert.All(RecoveryQuestions.All, q => Assert.True(RecoveryQuestions.IsValid(q))); }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("What is your name?")]
    public void OtherQuestions_AreNotValid(string? question) => Assert.False(RecoveryQuestions.IsValid(question));

    [Fact]
    public void DecoyQuestion_IsStable_AndIgnoresCase()
    {
        Assert.Equal(RecoveryQuestions.Decoy("nobody"), RecoveryQuestions.Decoy("nobody"));
        Assert.Equal(RecoveryQuestions.Decoy("Nobody"), RecoveryQuestions.Decoy("nobody"));
        Assert.Contains(RecoveryQuestions.Decoy("anyone"), RecoveryQuestions.All);
    }

    [Fact]
    public void Answers_AreComparedIgnoringCaseAndSurroundingSpaces()
    {
        var hash = RecoveryQuestions.Hash("  Pizza ");

        Assert.True(RecoveryQuestions.Verify("pizza", hash));
        Assert.True(RecoveryQuestions.Verify("PIZZA  ", hash));
        Assert.False(RecoveryQuestions.Verify("pasta", hash));
    }
}

public class AccountFlowTests
{
    [Fact]
    public async Task CorrectPassword_ReturnsNull_AndClearsEarlierFailures()
    {
        using var db = Db.New();
        var admin = Db.Admin(); admin.FailedLoginCount = 2;
        db.AdminAccounts.Add(admin); await db.SaveChangesAsync();

        var result = await AccountFlows.CheckPassword(db, admin, "Str0ng!Pass");

        Assert.Null(result);
        Assert.Equal(0, (await db.AdminAccounts.SingleAsync()).FailedLoginCount);
    }

    [Fact]
    public async Task UnknownUser_GetsTheSameUnauthorizedAnswerAsAWrongPassword()
    {
        using var db = Db.New();

        var unknown = await AccountFlows.CheckPassword(db, null, "whatever");

        var body = Assert.IsType<UnauthorizedObjectResult>(unknown);
        Assert.Equal("Invalid username or password.", body.Value);
    }

    [Fact]
    public async Task WrongPassword_IsUnauthorized_AndCounted_UntilTheThirdLocksTheAccount()
    {
        using var db = Db.New();
        var admin = Db.Admin(); db.AdminAccounts.Add(admin); await db.SaveChangesAsync();

        Assert.IsType<UnauthorizedObjectResult>(await AccountFlows.CheckPassword(db, admin, "bad1"));
        Assert.Equal(1, admin.FailedLoginCount);
        Assert.IsType<UnauthorizedObjectResult>(await AccountFlows.CheckPassword(db, admin, "bad2"));
        var third = await AccountFlows.CheckPassword(db, admin, "bad3");

        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(third).StatusCode);
        Assert.NotNull(admin.LockoutEnd);
    }

    [Fact]
    public async Task LockedAccount_IsRefused_EvenWithTheRightPassword()
    {
        using var db = Db.New();
        var admin = Db.Admin(); admin.LockoutEnd = DateTime.UtcNow.AddMinutes(10);
        db.AdminAccounts.Add(admin); await db.SaveChangesAsync();

        var result = await AccountFlows.CheckPassword(db, admin, "Str0ng!Pass");

        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public void RecoveryQuestion_ForUnknownUser_IsADecoy_ForKnownUser_TheirOwn()
    {
        var known = Assert.IsType<OkObjectResult>(AccountFlows.RecoveryQuestion(Db.Admin(), "alice"));
        var unknown = Assert.IsType<OkObjectResult>(AccountFlows.RecoveryQuestion(null, "ghost"));

        Assert.Equal(RecoveryQuestions.All[0], known.Value!.GetType().GetProperty("question")!.GetValue(known.Value));
        Assert.Equal(RecoveryQuestions.Decoy("ghost"), unknown.Value!.GetType().GetProperty("question")!.GetValue(unknown.Value));
    }

    private static RecoveryAnswerRequest Reset(string answer, string password) => new() { username = "alice", answer = answer, newPassword = password };

    [Fact]
    public async Task ResetPassword_WithTheRightAnswer_ChangesThePassword_AndClearsALock()
    {
        using var db = Db.New();
        var admin = Db.Admin(); admin.LockoutEnd = DateTime.UtcNow.AddMinutes(10); admin.FailedLoginCount = 0;
        db.AdminAccounts.Add(admin); await db.SaveChangesAsync();

        var result = await AccountFlows.ResetPassword(db, admin, Reset("  PIZZA", "N3w!Password"));

        Assert.IsType<OkObjectResult>(result);
        Assert.True(PasswordHasher.VerifyPassword("N3w!Password", admin.PasswordHash));
        Assert.Null(admin.LockoutEnd);
    }

    [Fact]
    public async Task ResetPassword_WithAWrongAnswer_IsUnauthorized_AndCountsTowardsTheLock()
    {
        using var db = Db.New();
        var admin = Db.Admin(); db.AdminAccounts.Add(admin); await db.SaveChangesAsync();

        Assert.IsType<UnauthorizedObjectResult>(await AccountFlows.ResetPassword(db, admin, Reset("pasta", "N3w!Password")));
        Assert.IsType<UnauthorizedObjectResult>(await AccountFlows.ResetPassword(db, admin, Reset("pasta", "N3w!Password")));
        var third = await AccountFlows.ResetPassword(db, admin, Reset("pasta", "N3w!Password"));

        Assert.Equal(StatusCodes.Status423Locked, Assert.IsType<ObjectResult>(third).StatusCode);
        Assert.True(PasswordHasher.VerifyPassword("Str0ng!Pass", admin.PasswordHash)); // unchanged
    }

    [Theory]
    [InlineData("pizza", "weak")]
    [InlineData("", "N3w!Password")]
    [InlineData("   ", "N3w!Password")]
    public async Task ResetPassword_NeedsAnAnswerAndAStrongPassword(string answer, string password)
    {
        using var db = Db.New();
        var admin = Db.Admin(); db.AdminAccounts.Add(admin); await db.SaveChangesAsync();

        Assert.IsType<BadRequestObjectResult>(await AccountFlows.ResetPassword(db, admin, Reset(answer, password)));
    }

    [Fact]
    public async Task ResetPassword_ForAnUnknownUser_IsUnauthorized()
    {
        using var db = Db.New();

        Assert.IsType<UnauthorizedObjectResult>(await AccountFlows.ResetPassword(db, null, Reset("pizza", "N3w!Password")));
    }
}
