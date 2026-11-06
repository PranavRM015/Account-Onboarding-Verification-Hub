using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using AccountSecurity;
using Data;
using InternalAccountsNamespace;
using JwtTokens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Tests;

public class JwtTokenServiceTests
{
    internal static string NewPem(int bits = 2048) => RSA.Create(bits).ExportPkcs8PrivateKeyPem();

    internal static IConfiguration Config(string? pem = null, Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?> { ["Jwt:PrivateKeyPem"] = pem ?? NewPem() };
        if (extra is not null) foreach (var pair in extra) values[pair.Key] = pair.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void AdminToken_CarriesNameRoleAdminIdAndAUniqueJti()
    {
        var service = new JwtTokenService(Config());

        var (token, expiresAt) = service.Issue("alice", Roles.Admin, 42);
        var jwt = Read(token);

        Assert.Equal("alice", jwt.Claims.Single(c => c.Type == "name").Value);
        Assert.Equal("admin", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal("42", jwt.Claims.Single(c => c.Type == JwtTokenService.AdminIdClaim).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtTokenService.CustomerIdClaim);
        Assert.False(string.IsNullOrEmpty(jwt.Id));
        Assert.True(expiresAt > DateTime.UtcNow.AddMinutes(59));
    }

    [Fact]
    public void CustomerToken_CarriesACustomerIdInsteadOfAnAdminId()
    {
        var jwt = Read(new JwtTokenService(Config()).Issue("carol", Roles.Customer, 7).Token);

        Assert.Equal("customer", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal("7", jwt.Claims.Single(c => c.Type == JwtTokenService.CustomerIdClaim).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtTokenService.AdminIdClaim);
    }

    [Fact]
    public void EveryTokenGetsItsOwnJti()
    {
        var service = new JwtTokenService(Config());

        Assert.NotEqual(Read(service.Issue("a", Roles.Admin, 1).Token).Id, Read(service.Issue("a", Roles.Admin, 1).Token).Id);
    }

    [Fact]
    public void IssuedToken_PassesTheServicesOwnValidationParameters()
    {
        var service = new JwtTokenService(Config());
        var token = service.Issue("alice", Roles.Admin, 1).Token;

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, service.ValidationParameters(), out _);

        Assert.Equal("alice", principal.Identity!.Name);
    }

    [Fact]
    public void TokenSignedByAnotherKey_IsRejected()
    {
        var other = new JwtTokenService(Config());
        var service = new JwtTokenService(Config());

        var forged = other.Issue("alice", Roles.Admin, 1).Token;

        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(forged, service.ValidationParameters(), out _));
    }

    [Fact]
    public void ExpiredToken_IsRejected()
    {
        var service = new JwtTokenService(Config(extra: new() { ["Jwt:ExpiryMinutes"] = "0.0001" })); // a 6 second lifetime
        var parameters = service.ValidationParameters();
        parameters.ClockSkew = TimeSpan.Zero; // the real service allows 30s of skew; remove it so the test stays short
        var token = service.Issue("alice", Roles.Admin, 1).Token;
        Thread.Sleep(TimeSpan.FromSeconds(8));

        Assert.Throws<SecurityTokenExpiredException>(() => new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _));
    }

    [Fact]
    public void TokenForAnotherAudienceOrIssuer_IsRejected()
    {
        var pem = NewPem();
        var issuer = new JwtTokenService(Config(pem, new() { ["Jwt:Audience"] = "someone-else" }));
        var checker = new JwtTokenService(Config(pem));

        var token = issuer.Issue("alice", Roles.Admin, 1).Token;

        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(token, checker.ValidationParameters(), out _));
    }

    [Fact]
    public void PublicKeySet_HasOnlyThePublicHalf_AndItsKeyIdMatchesTheTokens()
    {
        var service = new JwtTokenService(Config());
        var json = JsonSerializer.SerializeToElement(service.PublicKeySet());
        var key = json.GetProperty("keys")[0];

        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.False(key.TryGetProperty("d", out _));
        Assert.False(key.TryGetProperty("p", out _));
        Assert.Equal(key.GetProperty("kid").GetString(), Read(service.Issue("a", Roles.Admin, 1).Token).Header.Kid);
    }

    [Fact]
    public void ServiceStartedFromTheSameKey_PublishesTheSameKeyId()
    {
        var pem = NewPem();

        Assert.Equal(JsonSerializer.Serialize(new JwtTokenService(Config(pem)).PublicKeySet()), JsonSerializer.Serialize(new JwtTokenService(Config(pem)).PublicKeySet()));
    }

    [Fact]
    public void DiscoveryDocument_PointsAtTheJwksUnderTheGivenBaseUrl()
    {
        var doc = JsonSerializer.SerializeToElement(new JwtTokenService(Config()).DiscoveryDocument("http://identity:8080/"));

        Assert.Equal("AccountVerificationOnboardingHub", doc.GetProperty("issuer").GetString());
        Assert.Equal("http://identity:8080/.well-known/jwks.json", doc.GetProperty("jwks_uri").GetString());
    }

    [Fact]
    public void MissingPrivateKey_FailsAtStartup() =>
        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(new ConfigurationBuilder().Build()));

    [Fact]
    public void KeyUnder2048Bits_IsRefused() =>
        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(Config(NewPem(1024))));

    [Fact]
    public void NonPositiveExpiry_FallsBackTo60Minutes()
    {
        var (_, expires) = new JwtTokenService(Config(extra: new() { ["Jwt:ExpiryMinutes"] = "-5" })).Issue("a", Roles.Admin, 1);

        Assert.True(expires > DateTime.UtcNow.AddMinutes(59));
    }
}

public class InternalKeyAttributeTests
{
    private const string Key = "0123456789abcdef0123456789abcdef"; // 32 chars

    private static AuthorizationFilterContext Context(string? configuredKey, string? suppliedHeader)
    {
        var settings = new Dictionary<string, string?> { ["Internal:ApiKey"] = configuredKey };
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        if (suppliedHeader is not null) http.Request.Headers[InternalKeyAttribute.Header] = suppliedHeader;
        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>());
    }

    [Fact]
    public void MatchingKey_IsAllowedThrough()
    {
        var context = Context(Key, Key);
        new InternalKeyAttribute().OnAuthorization(context);
        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData("0123456789abcdef0123456789abcdeF")] // one character off
    public void MissingOrWrongKey_IsUnauthorized(string? supplied)
    {
        var context = Context(Key, supplied);
        new InternalKeyAttribute().OnAuthorization(context);
        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void WhenNoKeyIsConfigured_NothingGetsIn_EvenAnEmptyHeader()
    {
        var context = Context(null, "");
        new InternalKeyAttribute().OnAuthorization(context);
        Assert.IsType<UnauthorizedResult>(context.Result);
    }
}

public class InternalAccountsControllerTests
{
    private static (InternalAccountsController controller, IdentityDbContext db) New()
    {
        var db = Db.New();
        return (new InternalAccountsController(db, NullLogger<InternalAccountsController>.Instance), db);
    }

    private static CreateAccountRequest Account(Action<CreateAccountRequest>? change = null)
    {
        var request = new CreateAccountRequest
        {
            role = "admin", id = 1, username = "alice", password = "Str0ng!Pass",
            passwordRecoveryQuestion = RecoveryQuestions.All[0], passwordRecoveryAnswer = "pizza",
        };
        change?.Invoke(request);
        return request;
    }

    [Fact]
    public async Task CreatingAnAdmin_StoresOnlyHashes()
    {
        var (controller, db) = New();

        Assert.IsType<OkObjectResult>(await controller.Create(Account()));

        var stored = await db.AdminAccounts.SingleAsync();
        Assert.Equal("alice", stored.Username);
        Assert.NotEqual("Str0ng!Pass", stored.PasswordHash);
        Assert.NotEqual("pizza", stored.PasswordRecoveryAnswer);
    }

    [Fact]
    public async Task CreatingACustomer_GoesIntoTheCustomerTable()
    {
        var (controller, db) = New();

        Assert.IsType<OkObjectResult>(await controller.Create(Account(a => { a.role = " Customer "; a.id = 9; a.username = "carol"; })));

        Assert.Equal(9, (await db.CustomerAccounts.SingleAsync()).CustomerId);
        Assert.Empty(db.AdminAccounts);
    }

    [Theory]
    [InlineData("manager", 1, "alice", "Str0ng!Pass", 0, "pizza", "Role must be admin or customer.")]
    [InlineData("admin", 0, "alice", "Str0ng!Pass", 0, "pizza", "The id from the intake service is required.")]
    [InlineData("admin", 1, "al", "Str0ng!Pass", 0, "pizza", "A username of 3 to 40 characters is required.")]
    [InlineData("admin", 1, "weak", "weak", 0, "pizza", "Password must be")]
    [InlineData("admin", 1, "alice", "Str0ng!Pass", 0, "", "Choose one of the password recovery questions")]
    public async Task InvalidAccountRequests_AreRejectedWithABadRequest(string role, int id, string username, string password, int question, string answer, string messageStart)
    {
        var (controller, _) = New();

        var result = await controller.Create(Account(a => { a.role = role; a.id = id; a.username = username; a.password = password; a.passwordRecoveryQuestion = RecoveryQuestions.All[question]; a.passwordRecoveryAnswer = answer; }));

        Assert.StartsWith(messageStart, Assert.IsType<BadRequestObjectResult>(result).Value as string);
    }

    [Fact]
    public async Task UnknownRecoveryQuestion_IsRejected()
    {
        var (controller, _) = New();

        Assert.IsType<BadRequestObjectResult>(await controller.Create(Account(a => a.passwordRecoveryQuestion = "What is your quest?")));
    }

    [Fact]
    public async Task UsernameAlreadyTaken_IsAConflict_ForBothRoles()
    {
        var (controller, _) = New();
        await controller.Create(Account());
        await controller.Create(Account(a => { a.role = "customer"; }));

        Assert.IsType<ConflictObjectResult>(await controller.Create(Account(a => a.id = 2)));
        Assert.IsType<ConflictObjectResult>(await controller.Create(Account(a => { a.role = "customer"; a.id = 2; })));
    }

    [Fact]
    public async Task IdAlreadyHasAnAccount_IsAConflict()
    {
        var (controller, _) = New();
        await controller.Create(Account());

        Assert.IsType<ConflictObjectResult>(await controller.Create(Account(a => a.username = "someone-else")));
    }

    [Fact]
    public async Task SameUsernameInDifferentRoles_IsFine()
    {
        var (controller, _) = New();
        await controller.Create(Account());

        Assert.IsType<OkObjectResult>(await controller.Create(Account(a => a.role = "customer")));
    }

    [Fact]
    public async Task UsernameAvailable_ReflectsWhatExists()
    {
        var (controller, _) = New();
        await controller.Create(Account());

        Assert.False(Available(await controller.UsernameAvailable("admin", "alice")));
        Assert.True(Available(await controller.UsernameAvailable("admin", "bob")));
        Assert.True(Available(await controller.UsernameAvailable("customer", "alice")));
    }

    [Theory]
    [InlineData("", "alice")] [InlineData("admin", "  ")] [InlineData("boss", "alice")]
    public async Task UsernameAvailable_NeedsAKnownRoleAndAUsername(string role, string username)
    {
        var (controller, _) = New();

        Assert.IsType<BadRequestObjectResult>(await controller.UsernameAvailable(role, username));
    }

    private static bool Available(IActionResult result) =>
        (bool)Assert.IsType<OkObjectResult>(result).Value!.GetType().GetProperty("available")!.GetValue(Assert.IsType<OkObjectResult>(result).Value)!;
}
