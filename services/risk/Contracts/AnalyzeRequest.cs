using System.ComponentModel.DataAnnotations;

namespace Risk.Api.Contracts
{
    // Only fields consumed by the existing screening rules. No credentials, tax ID hash, or ticket ID.
    public class AnalyzeRequest
    {
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? MiddleName { get; set; }

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required, RegularExpression(@"^[0-9]{4}$")]
        public string TaxIdLastFourDigits { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string ResidentialAddress { get; set; } = string.Empty;

        [Required, RegularExpression(@"^[A-Z]{2}$")]
        public string CountryOfResidence { get; set; } = string.Empty;

        [Required, RegularExpression(@"^[A-Z]{2}$")]
        public string CountryOfCitizenship { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Occupation { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Employer { get; set; } = string.Empty;
    }
}
