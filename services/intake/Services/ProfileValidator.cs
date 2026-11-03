using Intake.DTOs;

namespace Intake.Services;

// Validates the customer profile fields that Intake is responsible for.
// These rules mirror the monolith's CustomerValidator and the check
// constraints on the Customers table.
//
// Credential fields (password, recovery question/answer) are intentionally
// NOT validated here — those belong to the Identity service.
public static class ProfileValidator
{
    private static readonly string[] ValidGenders    = { "Male", "Female", "Other" };
    private static readonly string[] ValidTaxIdTypes = { "SSN", "ITIN", "EIN" };

    // Returns null when the profile is valid, or an error message string.
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

        // Customer must be at least 18 years old (same rule as the monolith).
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

        if (string.IsNullOrWhiteSpace(dto.countryOfResidence) || dto.countryOfResidence.Length != 2)
            return "Country of residence must be a 2-letter ISO country code.";

        if (string.IsNullOrWhiteSpace(dto.countryOfCitizenship) || dto.countryOfCitizenship.Length != 2)
            return "Country of citizenship must be a 2-letter ISO country code.";

        if (string.IsNullOrWhiteSpace(dto.occupation))
            return "Occupation is required.";
        if (dto.occupation.Length > 100)
            return "Occupation cannot exceed 100 characters.";

        if (string.IsNullOrWhiteSpace(dto.employer))
            return "Employer is required.";
        if (dto.employer.Length > 100)
            return "Employer cannot exceed 100 characters.";

        return null; // all profile fields are valid
    }
}
