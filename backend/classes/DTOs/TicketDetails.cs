namespace TicketDtos
{
    public class CustomerDetailsDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string TaxIdType { get; set; } = string.Empty;
        public string ResidentialAddress { get; set; } = string.Empty;
        public string CountryOfResidence { get; set; } = string.Empty;
        public string CountryOfCitizenship { get; set; } = string.Empty;
        public string Occupation { get; set; } = string.Empty;
        public string Employer { get; set; } = string.Empty;
    }

    public class FlagDto
    {
        public int FlagId { get; set; }
        public string ViolationType { get; set; } = string.Empty;
        public DateTime RaisedAt { get; set; }
        public bool Cleared { get; set; }
        public DateTime? ClearedAt { get; set; }
        public string? ClearedBy { get; set; }
        public string? ClearedReason { get; set; }
    }

    public class VendorResponseDto
    {
        public string NameMatch { get; set; } = string.Empty;
        public string DateOfBirthMatch { get; set; } = string.Empty;
        public string AddressMatch { get; set; } = string.Empty;
        public string TaxIdMatch { get; set; } = string.Empty;
        public int Score { get; set; }
        public string RiskCodes { get; set; } = string.Empty;
        public DateTime VRTimeStamp { get; set; }
    }

    public class DecisionDto
    {
        public string DecisionType { get; set; } = string.Empty;
        public string DecidedBy { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime DecidedAt { get; set; }
    }

    public class TicketDetailsDto
    {
        public int TicketId { get; set; }
        public int CustomerId { get; set; }
        public string TicketStatus { get; set; } = string.Empty;
        public string? RiskRating { get; set; }
        public string AccountType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime VerificationDeadline { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string? FirstApprovedBy { get; set; }
        public bool Verified { get; set; }
        public CustomerDetailsDto Customer { get; set; } = new CustomerDetailsDto();
        public string? JointCustomerName { get; set; }
        public List<FlagDto> Flags { get; set; } = new List<FlagDto>();
        public VendorResponseDto? VendorResponse { get; set; }
        public DecisionDto? Decision { get; set; }
    }
}
