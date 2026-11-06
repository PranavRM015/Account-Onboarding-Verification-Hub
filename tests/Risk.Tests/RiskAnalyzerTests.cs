using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Risk.Api.Contracts;
using Risk.Api.Controllers;
using Risk.Api.Services;

namespace Risk.Tests;

// Covers every flag Risk can raise: VerificationFailed, OfacMatch, ClosedJurisdiction, ProhibitedIndustry.
public class RiskAnalyzerTests
{
    private static readonly RiskAnalyzer Analyzer = new(); // reads the real OFAC / deceased files copied next to the tests

    // A customer that trips nothing. Tests override one field at a time.
    private static AnalyzeRequest Clean(Action<AnalyzeRequest>? change = null)
    {
        var customer = new AnalyzeRequest
        {
            FirstName = "Zed", MiddleName = null, LastName = "Quillfeather",
            TaxIdLastFourDigits = "1234",
            ResidentialAddress = "1 Main Street, Austin TX",
            CountryOfResidence = "US", CountryOfCitizenship = "US",
            Occupation = "Teacher", Employer = "Springfield School District",
        };
        change?.Invoke(customer);
        return customer;
    }

    private static string[] DataLines(string file) =>
        File.ReadLines(Path.Combine(AppContext.BaseDirectory, file)).Skip(1).Where(l => l.Trim().Length > 0).ToArray();

    public static IEnumerable<object[]> DeceasedLastFours() => DataLines("DeceasedSSNs.csv").Select(l => new object[] { l.Trim() });
    public static IEnumerable<object[]> OfacEntries() => DataLines("OFACNames.csv").Select(l => l.Split(',')).Where(p => p.Length >= 3)
        .Select(p => new object[] { p[0].Trim(), p[1].Trim(), p[2].Trim() });

    // ---- clean ----------------------------------------------------------------------------------

    [Fact]
    public void CleanCustomer_RaisesNoFlags_AndIsLowRisk()
    {
        var result = Analyzer.Analyze(Clean());

        Assert.Equal("Low", result.RiskRating);
        Assert.Empty(result.Flags);
        Assert.Equal(new VendorResult("Match", "Match", "Match", "Match", 50, ""), result.VendorResponse);
    }

    // ---- VerificationFailed ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(DeceasedLastFours))]
    public void DeceasedTaxId_RaisesVerificationFailed(string lastFour)
    {
        var result = Analyzer.Analyze(Clean(c => c.TaxIdLastFourDigits = lastFour));

        Assert.Equal(new[] { "VerificationFailed" }, result.Flags);
        Assert.Equal("Medium", result.RiskRating);
        Assert.Equal("NoMatch", result.VendorResponse.TaxIdMatch);
        Assert.Equal("DeceasedSSN", result.VendorResponse.RiskCodes);
        Assert.Equal(30, result.VendorResponse.Score);
    }

    [Theory]
    [InlineData("correctional")]
    [InlineData("penitentiary")]
    [InlineData("state prison")]
    [InlineData("detention center")]
    [InlineData("jail")]
    [InlineData("JAIL")]
    public void CorrectionalAddress_RaisesVerificationFailed(string keyword)
    {
        var result = Analyzer.Analyze(Clean(c => c.ResidentialAddress = $"500 County {keyword} Road"));

        Assert.Equal(new[] { "VerificationFailed" }, result.Flags);
        Assert.Equal("Medium", result.RiskRating);
        Assert.Equal("Partial", result.VendorResponse.AddressMatch);
        Assert.Equal("CorrectionalAddress", result.VendorResponse.RiskCodes);
        Assert.Equal(40, result.VendorResponse.Score);
    }

    [Fact]
    public void DeceasedTaxIdAndCorrectionalAddress_RaiseOneFlagAndBothRiskCodes()
    {
        var result = Analyzer.Analyze(Clean(c => { c.TaxIdLastFourDigits = "0000"; c.ResidentialAddress = "1 Jail Lane"; }));

        Assert.Equal(new[] { "VerificationFailed" }, result.Flags);
        Assert.Equal("DeceasedSSN,CorrectionalAddress", result.VendorResponse.RiskCodes);
        Assert.Equal(20, result.VendorResponse.Score);
    }

    // ---- OfacMatch ------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(OfacEntries))]
    public void EveryOfacListEntry_RaisesOfacMatch_AndHighRisk(string first, string middle, string last)
    {
        var result = Analyzer.Analyze(Clean(c => { c.FirstName = first; c.MiddleName = middle.Length == 0 ? null : middle; c.LastName = last; }));

        Assert.Contains("OfacMatch", result.Flags);
        Assert.Equal("High", result.RiskRating);
    }

    [Fact]
    public void OfacMatch_IgnoresCaseAndSurroundingSpaces()
    {
        var result = Analyzer.Analyze(Clean(c => { c.FirstName = "  jAmEs "; c.MiddleName = " ROBERT"; c.LastName = "anderson  "; }));

        Assert.Contains("OfacMatch", result.Flags);
    }

    [Fact]
    public void OfacMatch_WithNoMiddleNameSupplied_StillMatchesAListedMiddleName()
    {
        var result = Analyzer.Analyze(Clean(c => { c.FirstName = "James"; c.MiddleName = null; c.LastName = "Anderson"; }));

        Assert.Contains("OfacMatch", result.Flags);
    }

    [Fact]
    public void OfacMatch_WithADifferentMiddleName_DoesNotMatch()
    {
        var result = Analyzer.Analyze(Clean(c => { c.FirstName = "James"; c.MiddleName = "Zachary"; c.LastName = "Anderson"; }));

        Assert.DoesNotContain("OfacMatch", result.Flags);
        Assert.Equal("Low", result.RiskRating);
    }

    [Fact]
    public void OfacMatch_ListedWithoutAMiddleName_MatchesAnyMiddleName()
    {
        var result = Analyzer.Analyze(Clean(c => { c.FirstName = "Maria"; c.MiddleName = "Anything"; c.LastName = "Gonzalez"; }));

        Assert.Contains("OfacMatch", result.Flags);
    }

    [Fact]
    public void SameLastNameButDifferentFirstName_DoesNotMatchOfac()
    {
        var result = Analyzer.Analyze(Clean(c => { c.FirstName = "Jamie"; c.LastName = "Anderson"; }));

        Assert.DoesNotContain("OfacMatch", result.Flags);
    }

    // ---- ClosedJurisdiction ---------------------------------------------------------------------

    [Theory]
    [InlineData("US", "US")]
    [InlineData("MX", "MX")]
    [InlineData("US", "MX")]
    [InlineData("MX", "US")]
    public void OpenCountries_DoNotRaiseClosedJurisdiction(string residence, string citizenship)
    {
        var result = Analyzer.Analyze(Clean(c => { c.CountryOfResidence = residence; c.CountryOfCitizenship = citizenship; }));

        Assert.DoesNotContain("ClosedJurisdiction", result.Flags);
        Assert.Equal("Low", result.RiskRating);
    }

    [Theory]
    [InlineData("KP", "US")]   // residence closed
    [InlineData("US", "IR")]   // citizenship closed
    [InlineData("CU", "SY")]   // both closed
    [InlineData("CA", "US")]   // anything other than US/MX is closed
    public void ClosedCountry_RaisesClosedJurisdiction_AndMediumRisk(string residence, string citizenship)
    {
        var result = Analyzer.Analyze(Clean(c => { c.CountryOfResidence = residence; c.CountryOfCitizenship = citizenship; }));

        Assert.Equal(new[] { "ClosedJurisdiction" }, result.Flags);
        Assert.Equal("Medium", result.RiskRating);
    }

    // ---- ProhibitedIndustry ---------------------------------------------------------------------

    [Theory]
    [InlineData("cannabis")]
    [InlineData("marijuana")]
    [InlineData("dispensary")]
    [InlineData("hemp")]
    [InlineData("thc")]
    [InlineData("cbd")]
    [InlineData("Cannabis")]
    public void ProhibitedOccupation_RaisesProhibitedIndustry_AndHighRisk(string keyword)
    {
        var result = Analyzer.Analyze(Clean(c => c.Occupation = $"Owner of a {keyword} business"));

        Assert.Equal(new[] { "ProhibitedIndustry" }, result.Flags);
        Assert.Equal("High", result.RiskRating);
    }

    [Theory]
    [InlineData("cannabis")]
    [InlineData("marijuana")]
    [InlineData("dispensary")]
    [InlineData("hemp")]
    [InlineData("thc")]
    [InlineData("cbd")]
    public void ProhibitedEmployer_RaisesProhibitedIndustry_AndHighRisk(string keyword)
    {
        var result = Analyzer.Analyze(Clean(c => c.Employer = $"Green {keyword} Co"));

        Assert.Equal(new[] { "ProhibitedIndustry" }, result.Flags);
        Assert.Equal("High", result.RiskRating);
    }

    // ---- combinations: rating follows the flags, flags come out in a fixed order -------------------

    // Builds a customer that raises exactly the flags in the set.
    private static AnalyzeRequest CustomerRaising(ISet<string> flags) => Clean(c =>
    {
        if (flags.Contains("VerificationFailed")) c.TaxIdLastFourDigits = "0000";
        if (flags.Contains("OfacMatch")) { c.FirstName = "James"; c.MiddleName = "Robert"; c.LastName = "Anderson"; }
        if (flags.Contains("ClosedJurisdiction")) c.CountryOfResidence = "KP";
        if (flags.Contains("ProhibitedIndustry")) c.Occupation = "Cannabis grower";
    });

    private static readonly string[] AllFlags = { "VerificationFailed", "OfacMatch", "ClosedJurisdiction", "ProhibitedIndustry" };

    public static IEnumerable<object[]> EveryFlagCombination() =>
        Enumerable.Range(0, 16).Select(mask => new object[] { mask });

    // Review refuses a ticket whose rating does not match its flags (High for OfacMatch / ProhibitedIndustry,
    // otherwise Medium for any flag, otherwise Low), so Risk must always agree with that rule.
    [Theory]
    [MemberData(nameof(EveryFlagCombination))]
    public void EveryCombinationOfFlags_ProducesExactlyThoseFlags_AndTheRatingReviewExpects(int mask)
    {
        var expected = AllFlags.Where((_, i) => (mask & (1 << i)) != 0).ToHashSet();

        var result = Analyzer.Analyze(CustomerRaising(expected));

        Assert.Equal(expected, result.Flags.ToHashSet());
        Assert.Equal(result.Flags.Length, result.Flags.Distinct().Count());
        string rating = expected.Contains("OfacMatch") || expected.Contains("ProhibitedIndustry") ? "High" : expected.Count > 0 ? "Medium" : "Low";
        Assert.Equal(rating, result.RiskRating);
    }

    [Theory]
    [MemberData(nameof(EveryFlagCombination))]
    public void VerificationFailed_AgreesWithTheVendorFindings_ThatReviewChecks(int mask)
    {
        var expected = AllFlags.Where((_, i) => (mask & (1 << i)) != 0).ToHashSet();

        var result = Analyzer.Analyze(CustomerRaising(expected));

        bool vendorFailed = result.VendorResponse.Score < 50 || result.VendorResponse.RiskCodes.Length > 0;
        Assert.Equal(vendorFailed, result.Flags.Contains("VerificationFailed"));
        Assert.InRange(result.VendorResponse.Score, 0, 50);
    }

    [Fact]
    public void AllFourFlags_AreReportedInOrder_AsHighRisk()
    {
        var result = Analyzer.Analyze(CustomerRaising(AllFlags.ToHashSet()));

        Assert.Equal(AllFlags, result.Flags);
        Assert.Equal("High", result.RiskRating);
    }

    [Fact]
    public void ClosedJurisdiction_DoesNotLowerAnOfacHighRating()
    {
        var result = Analyzer.Analyze(CustomerRaising(new HashSet<string> { "OfacMatch", "ClosedJurisdiction" }));

        Assert.Equal("High", result.RiskRating);
    }

    // ---- loading and request validation -------------------------------------------------------------

    [Fact]
    public void MissingScreeningFiles_FailLoudly_InsteadOfReturningACleanAssessment()
    {
        var empty = Directory.CreateTempSubdirectory().FullName;
        try { Assert.ThrowsAny<IOException>(() => new RiskAnalyzer(empty)); }
        finally { Directory.Delete(empty, true); }
    }

    [Fact]
    public void Controller_ReturnsTheAnalysis()
    {
        var response = new AnalyzeController(Analyzer).Analyze(CustomerRaising(new HashSet<string> { "OfacMatch" }));

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("High", Assert.IsType<AnalyzeResponse>(ok.Value).RiskRating);
    }

    private static bool IsValid(AnalyzeRequest request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), validateAllProperties: true);

    [Fact] public void ValidRequest_PassesValidation() => Assert.True(IsValid(Clean()));

    [Theory]
    [InlineData("123")] [InlineData("12345")] [InlineData("abcd")] [InlineData("")]
    public void TaxIdLastFour_MustBeFourDigits(string value) => Assert.False(IsValid(Clean(c => c.TaxIdLastFourDigits = value)));

    [Theory]
    [InlineData("us")] [InlineData("USA")] [InlineData("U")] [InlineData("")]
    public void CountryCodes_MustBeTwoUppercaseLetters(string value)
    {
        Assert.False(IsValid(Clean(c => c.CountryOfResidence = value)));
        Assert.False(IsValid(Clean(c => c.CountryOfCitizenship = value)));
    }

    [Fact] public void FirstName_IsRequired() => Assert.False(IsValid(Clean(c => c.FirstName = "")));
    [Fact] public void LastName_IsRequired() => Assert.False(IsValid(Clean(c => c.LastName = "")));
    [Fact] public void Address_IsRequiredAndLimitedTo200() { Assert.False(IsValid(Clean(c => c.ResidentialAddress = ""))); Assert.False(IsValid(Clean(c => c.ResidentialAddress = new string('a', 201)))); }
    [Fact] public void Occupation_IsRequired() => Assert.False(IsValid(Clean(c => c.Occupation = "")));
    [Fact] public void Employer_IsRequired() => Assert.False(IsValid(Clean(c => c.Employer = "")));
}
