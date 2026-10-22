using System;
using System.IO;
using Customers;
using Admins;
using Flags;
using Tickets;
using VendorResponses;

namespace MockVendor{
	public class Vendor
	{
		public Customer c {get; set;} = new Customer(); //fix this if it is erroneous
		public string? riskScore = null;
		public Vendor(Customer pCustomer){
			c.CustomerId = pCustomer.CustomerId;
			c.FirstName = pCustomer.FirstName;
			c.MiddleName = pCustomer.MiddleName;
			c.LastName = pCustomer.LastName;
			c.DateOfBirth = pCustomer.DateOfBirth;
			c.Gender = pCustomer.Gender;
			c.TaxIdType = pCustomer.TaxIdType;
			c.TaxIdHash = pCustomer.TaxIdHash;
			c.TaxIdLastFourDigits = pCustomer.TaxIdLastFourDigits;
			c.ResidentialAddress = pCustomer.ResidentialAddress;
			c.CountryOfResidence = pCustomer.CountryOfResidence;
			c.CountryOfCitizenship = pCustomer.CountryOfCitizenship;
			c.Occupation = pCustomer.Occupation;
			c.Employer = pCustomer.Employer;
		}
		
        private static int Penalty(string match) => match switch
        {
            "Partial" => 10,
            "NoMatch" => 20,
            _ => 0,
        };
		private static int Rank(string? rating) => rating switch
		{
			"High" => 3,
			"Medium" => 2,
			"Low" => 1,
			_ => 0,
		};

		private void Raise(string rating)
		{
			if(Rank(rating) > Rank(this.riskScore))
			{
				this.riskScore = rating;
			}
		}

		public string FinalRiskRating()
		{
			this.riskScore ??= "Low";
			return this.riskScore;
		}

		public Flag? CheckOFACVerification(Admin a, Customer c, int ticketId)
		{
			string filePath = Path.Combine(AppContext.BaseDirectory, "OFACNames.csv");
			if (!File.Exists(filePath))
			{
				throw new FileNotFoundException("The OFAC names file is missing.", filePath);
			}
			string customerMiddle = c.MiddleName?.Trim() ?? string.Empty;
			foreach(string line in File.ReadAllLines(filePath).Skip(1)){
				string[] fullName = line.Split(',');
				if(fullName.Length < 3){
					continue;
				}
				string middle = fullName[1].Trim();
				bool middleMatches = middle.Length == 0 || customerMiddle.Length == 0 || middle.ToLower() == customerMiddle.ToLower();
				if(fullName[0].Trim().ToLower() == c.FirstName.Trim().ToLower() && fullName[2].Trim().ToLower() == c.LastName.Trim().ToLower() && middleMatches){
					Raise("High");
					return new Flag(ticketId, "OfacMatch", a.AdminId);
				}
			}
			return null;
		}
		
		public Flag? CheckCountryTier(Admin a, Customer c, int ticketId){
			Flag? flag = null;
			string CountryOfResidence = c.CountryOfResidence;
			string CountryOfCitizenship = c.CountryOfCitizenship;
			if( !(CountryOfResidence == "US" || CountryOfResidence == "MX") ){
				flag = new Flag(ticketId, "ClosedJurisdiction", a.AdminId);
				Raise("Medium");
			}
			return flag;
		}
		
		public Flag? CheckIllicitSubstances(Admin a, Customer c, int ticketId){
			Flag? flag = null;
			string occupation = c.Occupation;
			string employer = c.Employer;
			if((occupation.ToLower()).Contains("cannabis") || (occupation.ToLower()).Contains("marijuana") ||
			(occupation.ToLower()).Contains("dispensary") || (occupation.ToLower()).Contains("hemp") ||
			(occupation.ToLower()).Contains("thc") || (occupation.ToLower()).Contains("cbd") ){
                flag = new Flag(ticketId, "ProhibitedIndustry" , a.AdminId);
				Raise("High");
			}

			if((employer.ToLower()).Contains("cannabis") || (employer.ToLower()).Contains("marijuana") ||
			(employer.ToLower()).Contains("dispensary") || (employer.ToLower()).Contains("hemp") ||
			(employer.ToLower()).Contains("thc") || (employer.ToLower()).Contains("cbd") ){
                flag = new Flag(ticketId, "ProhibitedIndustry" , a.AdminId);
				Raise("High");
			}
			return flag;
		}

        public VendorResponse Verify(Customer c, int ticketId)
        {
            string nameMatch = "Match";
            string dateOfBirthMatch = "Match";
            string addressMatch = "Match";
            string taxIdMatch = "Match";
            var riskCodes = new List<string>();

            string deceasedPath = Path.Combine(AppContext.BaseDirectory, "DeceasedSSNs.csv");
            if (!File.Exists(deceasedPath))
            {
                throw new FileNotFoundException("The deceased SSN file is missing.", deceasedPath);
            }
            if (File.ReadLines(deceasedPath).Skip(1).Any(l => l.Trim() == c.TaxIdLastFourDigits))
            {
                taxIdMatch = "NoMatch";
                riskCodes.Add("DeceasedSSN");
            }

            string[] facilityKeywords = { "correctional", "penitentiary", "state prison", "detention center" };
            string address = c.ResidentialAddress.ToLower();
            if (facilityKeywords.Any(k => address.Contains(k)))
            {
                addressMatch = "Partial";
                riskCodes.Add("CorrectionalAddress");
            }

            int score = Math.Max(0, 50 - Penalty(nameMatch) - Penalty(dateOfBirthMatch) - Penalty(addressMatch) - Penalty(taxIdMatch));
            return new VendorResponse(ticketId, nameMatch, dateOfBirthMatch, addressMatch, taxIdMatch, score, string.Join(",", riskCodes));
        }

		public Flag? CheckVerification(Admin a, VendorResponse response, int ticketId)
		{
			Flag? flag = null;
			if(response.Score < 50 || response.RiskCodes.Length > 0)
			{
				flag = new Flag(ticketId, "VerificationFailed", a.AdminId);
				Raise("Medium");
			}
			return flag;
		}
	}
}