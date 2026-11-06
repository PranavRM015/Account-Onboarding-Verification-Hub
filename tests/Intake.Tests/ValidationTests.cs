using Intake.DTOs;
using Intake.Services;

namespace Intake.Tests;

public class ProfileValidatorCustomerTests
{
    private static CustomerSignupDto Customer(Action<CustomerSignupDto>? change = null)
    {
        var dto = new CustomerSignupDto
        {
            firstName = "Zed", middleName = null, lastName = "Quillfeather",
            dateOfBirth = new DateTime(1990, 5, 17), gender = "Male",
            taxIdType = "SSN", taxIdNumber = "123-45-6789",
            residentialAddress = "1 Main Street", countryOfResidence = "US", countryOfCitizenship = "US",
            occupation = "Teacher", employer = "School",
        };
        change?.Invoke(dto);
        return dto;
    }

    [Fact] public void ValidCustomer_HasNoError() => Assert.Null(ProfileValidator.Validate(Customer()));

    [Theory]
    [InlineData("", "First name is required.")]
    [InlineData("   ", "First name is required.")]
    public void FirstName_IsRequired(string value, string error) => Assert.Equal(error, ProfileValidator.Validate(Customer(c => c.firstName = value)));
    [Fact] public void FirstName_Max50() => Assert.Equal("First name cannot exceed 50 characters.", ProfileValidator.Validate(Customer(c => c.firstName = new string('a', 51))));
    [Fact] public void MiddleName_Max50() => Assert.Equal("Middle name cannot exceed 50 characters.", ProfileValidator.Validate(Customer(c => c.middleName = new string('a', 51))));
    [Fact] public void LastName_IsRequired() => Assert.Equal("Last name is required.", ProfileValidator.Validate(Customer(c => c.lastName = "")));
    [Fact] public void LastName_Max50() => Assert.Equal("Last name cannot exceed 50 characters.", ProfileValidator.Validate(Customer(c => c.lastName = new string('a', 51))));

    [Fact]
    public void MustBeAtLeast18_ToTheDay()
    {
        var today = DateTime.UtcNow.Date;
        Assert.Null(ProfileValidator.Validate(Customer(c => c.dateOfBirth = today.AddYears(-18))));
        Assert.Equal("Customer must be at least 18 years old.", ProfileValidator.Validate(Customer(c => c.dateOfBirth = today.AddYears(-18).AddDays(1))));
    }

    [Theory] [InlineData("Male")] [InlineData("Female")] [InlineData("Other")]
    public void KnownGenders_Pass(string gender) => Assert.Null(ProfileValidator.Validate(Customer(c => c.gender = gender)));
    [Theory] [InlineData("male")] [InlineData("")] [InlineData("Unknown")]
    public void UnknownGenders_Fail(string gender) => Assert.Equal("Gender must be Male, Female, or Other.", ProfileValidator.Validate(Customer(c => c.gender = gender)));

    [Theory] [InlineData("SSN")] [InlineData("ssn")] [InlineData("ITIN")] [InlineData("EIN")]
    public void KnownTaxIdTypes_Pass_IgnoringCase(string type) => Assert.Null(ProfileValidator.Validate(Customer(c => c.taxIdType = type)));
    [Theory] [InlineData("")] [InlineData("TIN")]
    public void UnknownTaxIdTypes_Fail(string type) => Assert.Equal("Tax ID type must be SSN, ITIN, or EIN.", ProfileValidator.Validate(Customer(c => c.taxIdType = type)));

    [Theory] [InlineData("")] [InlineData("   ")] [InlineData("--- ---")]
    public void TaxIdNumber_IsRequired(string value) => Assert.Equal("A tax ID number is required.", ProfileValidator.Validate(Customer(c => c.taxIdNumber = value)));
    [Theory] [InlineData("1-2-3")] [InlineData("12")]
    public void TaxIdNumber_NeedsAtLeastFourCharacters(string value) => Assert.Equal("Tax ID number must contain at least 4 digits.", ProfileValidator.Validate(Customer(c => c.taxIdNumber = value)));
    [Fact] public void TaxIdNumber_DashesAreIgnored() => Assert.Null(ProfileValidator.Validate(Customer(c => c.taxIdNumber = "1-2-3-4")));

    [Fact] public void Address_IsRequired() => Assert.Equal("Residential address is required.", ProfileValidator.Validate(Customer(c => c.residentialAddress = " ")));
    [Fact] public void Address_Max200() => Assert.Equal("Residential address cannot exceed 200 characters.", ProfileValidator.Validate(Customer(c => c.residentialAddress = new string('a', 201))));

    [Theory] [InlineData("US")] [InlineData("us")] [InlineData(" mx ")] [InlineData("KP")]
    public void CountryCodes_AreCheckedAfterTrimmingAndUppercasing(string code)
    {
        Assert.Null(ProfileValidator.Validate(Customer(c => c.countryOfResidence = code)));
        Assert.Null(ProfileValidator.Validate(Customer(c => c.countryOfCitizenship = code)));
    }
    [Theory] [InlineData("")] [InlineData("USA")] [InlineData("ZZ")] [InlineData("1A")]
    public void BadCountryCodes_Fail(string code)
    {
        Assert.Equal("Country of residence must be a valid two-letter country code.", ProfileValidator.Validate(Customer(c => c.countryOfResidence = code)));
        Assert.Equal("Country of citizenship must be a valid two-letter country code.", ProfileValidator.Validate(Customer(c => c.countryOfCitizenship = code)));
    }

    [Fact] public void Occupation_IsRequiredAndMax100() { Assert.Equal("Occupation is required.", ProfileValidator.Validate(Customer(c => c.occupation = ""))); Assert.Equal("Occupation cannot exceed 100 characters.", ProfileValidator.Validate(Customer(c => c.occupation = new string('a', 101)))); }
    [Fact] public void Employer_IsRequiredAndMax100() { Assert.Equal("Employer is required.", ProfileValidator.Validate(Customer(c => c.employer = ""))); Assert.Equal("Employer cannot exceed 100 characters.", ProfileValidator.Validate(Customer(c => c.employer = new string('a', 101)))); }

    // The profile is accepted even when Risk will later flag it: screening decides flags, validation only checks shape.
    [Theory]
    [InlineData("KP", "US")]                 // ClosedJurisdiction
    public void ProfilesThatRiskWillFlag_StillPassValidation(string residence, string citizenship) =>
        Assert.Null(ProfileValidator.Validate(Customer(c => { c.countryOfResidence = residence; c.countryOfCitizenship = citizenship; })));
    [Fact] public void ProhibitedIndustryProfile_StillPassesValidation() => Assert.Null(ProfileValidator.Validate(Customer(c => c.occupation = "Cannabis grower")));
    [Fact] public void OfacNamedProfile_StillPassesValidation() => Assert.Null(ProfileValidator.Validate(Customer(c => { c.firstName = "James"; c.middleName = "Robert"; c.lastName = "Anderson"; })));
    [Fact] public void DeceasedTaxIdProfile_StillPassesValidation() => Assert.Null(ProfileValidator.Validate(Customer(c => c.taxIdNumber = "123450000")));
}

public class ProfileValidatorAdminTests
{
    private static AdminSignupDto Admin(Action<AdminSignupDto>? change = null)
    {
        var dto = new AdminSignupDto
        {
            firstName = "Alice", lastName = "Admin", dateOfBirth = new DateTime(1985, 3, 3), gender = "Female",
            ssn = "123-45-6789", isCitizen = true, countryOfCitizenship = "US",
        };
        change?.Invoke(dto);
        return dto;
    }

    [Fact] public void ValidAdmin_HasNoError() => Assert.Null(ProfileValidator.Validate(Admin()));
    [Fact] public void FirstName_IsRequired() => Assert.Equal("First name is required.", ProfileValidator.Validate(Admin(a => a.firstName = "")));
    [Fact] public void LastName_IsRequired() => Assert.Equal("Last name is required.", ProfileValidator.Validate(Admin(a => a.lastName = "")));
    [Fact] public void MustBeAtLeast18() => Assert.Equal("Admin must be at least 18 years old.", ProfileValidator.Validate(Admin(a => a.dateOfBirth = DateTime.UtcNow.Date.AddYears(-17))));
    [Fact] public void Gender_MustBeKnown() => Assert.Equal("Gender must be Male, Female, or Other.", ProfileValidator.Validate(Admin(a => a.gender = "x")));

    [Theory] [InlineData("123456789")] [InlineData("123-45-6789")] [InlineData("123 45 6789")]
    public void NineDigitSsn_Passes(string ssn) => Assert.Null(ProfileValidator.Validate(Admin(a => a.ssn = ssn)));
    [Theory] [InlineData("")] [InlineData("12345678")] [InlineData("1234567890")] [InlineData("12345678A")]
    public void SsnMustBeExactlyNineDigits(string ssn) => Assert.Equal("An SSN of exactly 9 digits is required.", ProfileValidator.Validate(Admin(a => a.ssn = ssn)));

    [Fact] public void CitizenshipCountry_MustBeValid() => Assert.Equal("Country of citizenship must be a valid two-letter country code.", ProfileValidator.Validate(Admin(a => a.countryOfCitizenship = "ZZ")));
}

public class TaxIdHasherTests
{
    [Fact]
    public void Hash_Is32Bytes_AndRepeatable()
    {
        var first = TaxIdHasher.Hash("123-45-6789", "pepper");

        Assert.Equal(32, first.Length);
        Assert.Equal(first, TaxIdHasher.Hash("123-45-6789", "pepper"));
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("123-45-6789")]
    [InlineData(" 123 45 6789 ")]
    public void FormattingOfTheNumber_DoesNotChangeTheHash(string formatted) =>
        Assert.Equal(TaxIdHasher.Hash("123456789", "pepper"), TaxIdHasher.Hash(formatted, "pepper"));

    [Fact] public void LettersAreCaseInsensitive() => Assert.Equal(TaxIdHasher.Hash("ab-123", "p"), TaxIdHasher.Hash("AB123", "p"));
    [Fact] public void DifferentNumbers_GiveDifferentHashes() => Assert.NotEqual(TaxIdHasher.Hash("123456789", "p"), TaxIdHasher.Hash("123456788", "p"));
    [Fact] public void DifferentPeppers_GiveDifferentHashes() => Assert.NotEqual(TaxIdHasher.Hash("123456789", "one"), TaxIdHasher.Hash("123456789", "two"));
}

public class CountryCodesTests
{
    [Theory] [InlineData("US")] [InlineData("MX")] [InlineData("KP")] [InlineData("IR")] [InlineData("CU")] [InlineData("SY")]
    public void CodesRiskTreatsAsClosedOrOpen_AreAllSelectable(string code) => Assert.Contains(code, CountryCodes.All);
    [Theory] [InlineData("XX")] [InlineData("us")] [InlineData("")]
    public void UnknownOrLowercaseCodes_AreNotInTheList(string code) => Assert.DoesNotContain(code, CountryCodes.All);
}
