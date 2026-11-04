using System.ComponentModel.DataAnnotations;
using TicketDtos;

namespace Review.Api.Contracts
{
    public class CreateTicketRequest : IValidatableObject
    {
        public Guid RequestId { get; set; }
        [Range(1, int.MaxValue)]
        public int CustomerId { get; set; }
        [Required, RegularExpression("^(Individual|Joint)$")]
        public string AccountType { get; set; } = string.Empty;
        [Range(1, int.MaxValue)]
        public int? JointCustomerId { get; set; }
        [MaxLength(151)]
        public string? JointCustomerName { get; set; }
        [Required]
        public CustomerDetailsDto Customer { get; set; } = null!;
        [Required, RegularExpression("^(Low|Medium|High)$")]
        public string RiskRating { get; set; } = string.Empty;
        [Required, MaxLength(4)]
        public string[] Flags { get; set; } = Array.Empty<string>();
        [Required]
        public VendorAssessment VendorResponse { get; set; } = null!;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Customer is not null && (Customer.DateOfBirth < new DateTime(1900, 1, 1) || Customer.DateOfBirth > DateTime.UtcNow.Date.AddYears(-18)))
            {
                yield return new ValidationResult("Customer dateOfBirth must describe an adult born on or after 1900-01-01.");
            }
            if (RequestId == Guid.Empty) { yield return new ValidationResult("A nonempty requestId is required.", new[] { nameof(RequestId) }); }
            if (AccountType == "Joint" && (JointCustomerId is null || JointCustomerId == CustomerId || string.IsNullOrWhiteSpace(JointCustomerName)))
            {
                yield return new ValidationResult("Joint accounts require a different customer ID and a jointCustomerName snapshot.");
            }
            if (AccountType == "Individual" && (JointCustomerId is not null || JointCustomerName is not null))
            {
                yield return new ValidationResult("Individual accounts cannot have a joint customer.");
            }
            if (Flags is not null)
            {
                if (Flags.Any(f => !global::Flags.FlagValues.ViolationTypes.Contains(f)) || Flags.Distinct().Count() != Flags.Length)
                {
                    yield return new ValidationResult("Flags must be unique known violation types.");
                }
                string expected = Flags.Any(f => f == "OfacMatch" || f == "ProhibitedIndustry") ? "High" : Flags.Length > 0 ? "Medium" : "Low";
                if (RiskRating != expected) { yield return new ValidationResult("Risk rating does not match the supplied flags."); }
                if (VendorResponse is not null && VendorResponse.RiskCodes is not null && Flags.Contains("VerificationFailed") != (VendorResponse.Score < 50 || VendorResponse.RiskCodes.Length > 0))
                {
                    yield return new ValidationResult("VerificationFailed must match the vendor findings.");
                }
            }
        }
    }

    public class VendorAssessment
    {
        [Required, RegularExpression("^(Match|Partial|NoMatch)$")]
        public string NameMatch { get; set; } = string.Empty;
        [Required, RegularExpression("^(Match|Partial|NoMatch)$")]
        public string DateOfBirthMatch { get; set; } = string.Empty;
        [Required, RegularExpression("^(Match|Partial|NoMatch)$")]
        public string AddressMatch { get; set; } = string.Empty;
        [Required, RegularExpression("^(Match|Partial|NoMatch)$")]
        public string TaxIdMatch { get; set; } = string.Empty;
        [Range(0, 50)]
        public int Score { get; set; }
        [Required(AllowEmptyStrings = true), MaxLength(200)]
        public string RiskCodes { get; set; } = string.Empty;
    }
}
