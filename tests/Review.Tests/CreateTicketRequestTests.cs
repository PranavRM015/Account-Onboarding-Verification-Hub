using System.ComponentModel.DataAnnotations;
using Review.Api.Contracts;
using TicketDtos;

namespace Review.Tests;

// The rules Review applies to a ticket that Intake asks it to open, including every flag.
public class CreateTicketRequestTests
{
    private static readonly string[] AllFlags = { "VerificationFailed", "OfacMatch", "ClosedJurisdiction", "ProhibitedIndustry" };

    // A consistent request carrying exactly these flags (rating and vendor findings follow the flags).
    private static CreateTicketRequest Request(IEnumerable<string>? flags = null, Action<CreateTicketRequest>? change = null)
    {
        var list = (flags ?? Array.Empty<string>()).ToArray();
        bool failed = list.Contains("VerificationFailed");
        var request = new CreateTicketRequest
        {
            RequestId = Guid.NewGuid(),
            CustomerId = 7,
            AccountType = "Individual",
            Customer = new CustomerDetailsDto
            {
                FirstName = "Zed", LastName = "Quillfeather", DateOfBirth = new DateTime(1990, 1, 1), Gender = "Male", TaxIdType = "SSN",
                ResidentialAddress = "1 Main Street", CountryOfResidence = "US", CountryOfCitizenship = "US",
                Occupation = "Teacher", Employer = "School",
            },
            Flags = list,
            RiskRating = ExpectedRating(list),
            VendorResponse = new VendorAssessment
            {
                NameMatch = "Match", DateOfBirthMatch = "Match", AddressMatch = "Match", TaxIdMatch = failed ? "NoMatch" : "Match",
                Score = failed ? 30 : 50, RiskCodes = failed ? "DeceasedSSN" : "",
            },
        };
        change?.Invoke(request);
        return request;
    }

    private static string ExpectedRating(string[] flags) =>
        flags.Contains("OfacMatch") || flags.Contains("ProhibitedIndustry") ? "High" : flags.Length > 0 ? "Medium" : "Low";

    // ASP.NET's model validation also walks into the nested objects; Validator alone does not, so do that here.
    private static List<ValidationResult> Validate(CreateTicketRequest request)
    {
        var results = new List<ValidationResult>();
        foreach (var part in new object?[] { request, request.Customer, request.VendorResponse })
        {
            if (part is not null) Validator.TryValidateObject(part, new ValidationContext(part), results, validateAllProperties: true);
        }
        return results;
    }

    private static bool IsValid(CreateTicketRequest request) => Validate(request).Count == 0;

    public static IEnumerable<object[]> EveryFlagCombination() => Enumerable.Range(0, 16).Select(mask => new object[] { mask });
    private static string[] Subset(int mask) => AllFlags.Where((_, i) => (mask & (1 << i)) != 0).ToArray();

    // ---- flags ------------------------------------------------------------------------------------

    [Fact] public void NoFlags_LowRating_IsValid() => Assert.True(IsValid(Request()));

    [Theory]
    [InlineData("VerificationFailed")]
    [InlineData("OfacMatch")]
    [InlineData("ClosedJurisdiction")]
    [InlineData("ProhibitedIndustry")]
    public void EachKnownFlag_OnItsOwn_IsAccepted(string flag) => Assert.True(IsValid(Request(new[] { flag })));

    [Theory]
    [MemberData(nameof(EveryFlagCombination))]
    public void EveryFlagCombination_WithTheMatchingRating_IsAccepted(int mask) => Assert.True(IsValid(Request(Subset(mask))));

    [Theory]
    [MemberData(nameof(EveryFlagCombination))]
    public void EveryFlagCombination_WithAWrongRating_IsRejected(int mask)
    {
        var flags = Subset(mask);
        foreach (var wrong in new[] { "Low", "Medium", "High" }.Where(r => r != ExpectedRating(flags)))
        {
            var results = Validate(Request(flags, r => r.RiskRating = wrong));
            Assert.Contains(results, r => r.ErrorMessage == "Risk rating does not match the supplied flags.");
        }
    }

    [Theory]
    [InlineData("OfacMatch")]
    [InlineData("ProhibitedIndustry")]
    public void OfacAndProhibitedIndustry_RequireHighRating(string flag)
    {
        Assert.True(IsValid(Request(new[] { flag }, r => r.RiskRating = "High")));
        Assert.False(IsValid(Request(new[] { flag }, r => r.RiskRating = "Medium")));
    }

    [Theory]
    [InlineData("VerificationFailed")]
    [InlineData("ClosedJurisdiction")]
    public void VerificationFailedAndClosedJurisdiction_RequireMediumRating(string flag)
    {
        Assert.True(IsValid(Request(new[] { flag })));
        Assert.False(IsValid(Request(new[] { flag }, r => r.RiskRating = "High")));
        Assert.False(IsValid(Request(new[] { flag }, r => r.RiskRating = "Low")));
    }

    [Fact] public void UnknownFlag_IsRejected() => Assert.False(IsValid(Request(new[] { "SomethingElse" }, r => r.RiskRating = "Medium")));
    [Fact] public void FlagNames_AreCaseSensitive() => Assert.False(IsValid(Request(new[] { "ofacmatch" }, r => r.RiskRating = "High")));
    [Fact] public void DuplicateFlag_IsRejected() => Assert.False(IsValid(Request(new[] { "OfacMatch", "OfacMatch" })));
    [Fact] public void MoreThanFourFlags_AreRejected() => Assert.False(IsValid(Request(AllFlags.Append("OfacMatch"))));

    [Fact]
    public void VerificationFailed_WithCleanVendorFindings_IsRejected()
    {
        var results = Validate(Request(new[] { "VerificationFailed" }, r => { r.VendorResponse.Score = 50; r.VendorResponse.RiskCodes = ""; }));

        Assert.Contains(results, r => r.ErrorMessage == "VerificationFailed must match the vendor findings.");
    }

    [Fact]
    public void FailedVendorScore_WithoutTheVerificationFailedFlag_IsRejected()
    {
        Assert.False(IsValid(Request(null, r => { r.VendorResponse.Score = 40; })));
        Assert.False(IsValid(Request(null, r => { r.VendorResponse.RiskCodes = "CorrectionalAddress"; })));
    }

    [Fact]
    public void VerificationFailed_IsAccepted_ForEitherVendorFailureReason()
    {
        Assert.True(IsValid(Request(new[] { "VerificationFailed" }, r => { r.VendorResponse.Score = 40; r.VendorResponse.RiskCodes = ""; })));
        Assert.True(IsValid(Request(new[] { "VerificationFailed" }, r => { r.VendorResponse.Score = 50; r.VendorResponse.RiskCodes = "CorrectionalAddress"; })));
    }

    // ---- request id, account type, customer ------------------------------------------------------------

    [Fact] public void EmptyRequestId_IsRejected() => Assert.False(IsValid(Request(null, r => r.RequestId = Guid.Empty)));
    [Fact] public void CustomerId_MustBePositive() => Assert.False(IsValid(Request(null, r => r.CustomerId = 0)));
    [Theory]
    [InlineData("Business")] [InlineData("individual")] [InlineData("")]
    public void AccountType_MustBeIndividualOrJoint(string type) => Assert.False(IsValid(Request(null, r => r.AccountType = type)));

    [Fact]
    public void JointAccount_WithADifferentCustomerAndName_IsValid() =>
        Assert.True(IsValid(Request(null, r => { r.AccountType = "Joint"; r.JointCustomerId = 8; r.JointCustomerName = "Ann Lee"; })));

    [Fact] public void JointAccount_WithoutASecondCustomer_IsRejected() =>
        Assert.False(IsValid(Request(null, r => { r.AccountType = "Joint"; r.JointCustomerName = "Ann Lee"; })));
    [Fact] public void JointAccount_WithTheSameCustomerTwice_IsRejected() =>
        Assert.False(IsValid(Request(null, r => { r.AccountType = "Joint"; r.JointCustomerId = r.CustomerId; r.JointCustomerName = "Ann Lee"; })));
    [Fact] public void JointAccount_WithoutANameSnapshot_IsRejected() =>
        Assert.False(IsValid(Request(null, r => { r.AccountType = "Joint"; r.JointCustomerId = 8; })));
    [Fact] public void IndividualAccount_WithASecondCustomer_IsRejected() =>
        Assert.False(IsValid(Request(null, r => r.JointCustomerId = 8)));
    [Fact] public void IndividualAccount_WithAJointName_IsRejected() =>
        Assert.False(IsValid(Request(null, r => r.JointCustomerName = "Ann Lee")));

    [Fact]
    public void CustomerMustBeAnAdult()
    {
        var eighteenToday = DateTime.UtcNow.Date.AddYears(-18);
        Assert.True(IsValid(Request(null, r => r.Customer.DateOfBirth = eighteenToday)));
        Assert.False(IsValid(Request(null, r => r.Customer.DateOfBirth = eighteenToday.AddDays(1))));
    }

    [Fact]
    public void CustomerBornBefore1900_IsRejected()
    {
        Assert.True(IsValid(Request(null, r => r.Customer.DateOfBirth = new DateTime(1900, 1, 1))));
        Assert.False(IsValid(Request(null, r => r.Customer.DateOfBirth = new DateTime(1899, 12, 31))));
    }

    [Theory]
    [InlineData("Unknown")] [InlineData("male")]
    public void Gender_MustBeKnown(string gender) => Assert.False(IsValid(Request(null, r => r.Customer.Gender = gender)));
    [Theory]
    [InlineData("TIN")] [InlineData("ssn")]
    public void TaxIdType_MustBeKnown(string type) => Assert.False(IsValid(Request(null, r => r.Customer.TaxIdType = type)));
    [Theory]
    [InlineData("us")] [InlineData("USA")]
    public void CustomerCountryCodes_MustBeUppercaseTwoLetters(string code)
    {
        Assert.False(IsValid(Request(null, r => r.Customer.CountryOfResidence = code)));
        Assert.False(IsValid(Request(null, r => r.Customer.CountryOfCitizenship = code)));
    }

    // ---- vendor response ---------------------------------------------------------------------------

    [Theory]
    [InlineData("Maybe")] [InlineData("match")] [InlineData("")]
    public void VendorMatchValues_MustBeKnown(string value)
    {
        Assert.False(IsValid(Request(null, r => r.VendorResponse.NameMatch = value)));
        Assert.False(IsValid(Request(null, r => r.VendorResponse.DateOfBirthMatch = value)));
        Assert.False(IsValid(Request(null, r => r.VendorResponse.AddressMatch = value)));
        Assert.False(IsValid(Request(null, r => r.VendorResponse.TaxIdMatch = value)));
    }

    [Theory]
    [InlineData(-1)] [InlineData(51)]
    public void VendorScore_MustBeBetween0And50(int score) => Assert.False(IsValid(Request(null, r => r.VendorResponse.Score = score)));

    [Fact] public void VendorRiskCodes_AreLimitedTo200Characters() => Assert.False(IsValid(Request(null, r => r.VendorResponse.RiskCodes = new string('x', 201))));
}
