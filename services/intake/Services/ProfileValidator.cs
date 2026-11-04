using Intake.DTOs;

namespace Intake.Services;

// Validates customer profile fields. Credential validation (password strength,
// recovery question) is Identity's responsibility — not checked here.
public static class ProfileValidator
{
    private static readonly string[] ValidGenders    = { "Male", "Female", "Other" };
    private static readonly string[] ValidTaxIdTypes = { "SSN", "ITIN", "EIN" };

    // Returns null when valid, or an error message string.
    public static string? Validate(CustomerSignupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.firstName))
            return "First name is required.";
        if (dto.firstName.Length > 50)
            return "First name cannot exceed 50 characters.";

        if (dto.middleName is not null && dto.middleName.Length > 50)
            return "Middle name cannot exceed 50 characters.";

        if (string.IsNullOrWhiteSpace(dto.lastName))
            return "Last name is required.";
        if (dto.lastName.Length > 50)
            return "Last name cannot exceed 50 characters.";

        if (dto.dateOfBirth > DateTime.UtcNow.Date.AddYears(-18))
            return "Customer must be at least 18 years old.";

        if (!ValidGenders.Contains(dto.gender))
            return "Gender must be Male, Female, or Other.";

        if (!ValidTaxIdTypes.Contains(dto.taxIdType?.ToUpperInvariant()))
            return "Tax ID type must be SSN, ITIN, or EIN.";

        var taxDigits = string.IsNullOrWhiteSpace(dto.taxIdNumber)
            ? string.Empty
            : new string(dto.taxIdNumber.Where(char.IsLetterOrDigit).ToArray());

        if (taxDigits.Length == 0)
            return "A tax ID number is required.";

        if (taxDigits.Length < 4)
            return "Tax ID number must contain at least 4 digits.";

        if (string.IsNullOrWhiteSpace(dto.residentialAddress))
            return "Residential address is required.";
        if (dto.residentialAddress.Length > 200)
            return "Residential address cannot exceed 200 characters.";

        if (!IsCountryCode(dto.countryOfResidence))
            return "Country of residence must be a valid two-letter country code.";

        if (!IsCountryCode(dto.countryOfCitizenship))
            return "Country of citizenship must be a valid two-letter country code.";

        if (string.IsNullOrWhiteSpace(dto.occupation))
            return "Occupation is required.";
        if (dto.occupation.Length > 100)
            return "Occupation cannot exceed 100 characters.";

        if (string.IsNullOrWhiteSpace(dto.employer))
            return "Employer is required.";
        if (dto.employer.Length > 100)
            return "Employer cannot exceed 100 characters.";

        return null;
    }

    // Returns null when valid, or an error message string.
    public static string? Validate(AdminSignupDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.firstName))
            return "First name is required.";
        if (dto.firstName.Length > 50)
            return "First name cannot exceed 50 characters.";

        if (dto.middleName is not null && dto.middleName.Length > 50)
            return "Middle name cannot exceed 50 characters.";

        if (string.IsNullOrWhiteSpace(dto.lastName))
            return "Last name is required.";
        if (dto.lastName.Length > 50)
            return "Last name cannot exceed 50 characters.";

        if (dto.dateOfBirth > DateTime.UtcNow.Date.AddYears(-18))
            return "Admin must be at least 18 years old.";

        if (!ValidGenders.Contains(dto.gender))
            return "Gender must be Male, Female, or Other.";

        var ssn = new string((dto.ssn ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
        if (ssn.Length != 9 || !ssn.All(char.IsDigit))
            return "An SSN of exactly 9 digits is required.";

        if (!IsCountryCode(dto.countryOfCitizenship))
            return "Country of citizenship must be a valid two-letter country code.";

        return null;
    }

    // Both signup controllers trim and upper-case the code before saving, so check it the same way.
    private static bool IsCountryCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && CountryCodes.All.Contains(code.Trim().ToUpperInvariant());
}
