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
		public string riskScore;
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
		public void checkOFACVerification(Admin a, Customer c, int ticketId)
		{
			string filePath = Path.Combine(Directory.GetCurrentDirectory(), "OFACNames.csv");
			if (File.Exists(filePath))
			{
				string[] lines = File.ReadAllLines(filePath);
				//List<List<string>> fuzzyNames = new List<List<string>>();
				foreach(string line in lines){
					List<string> fullName = line.Split(',').ToList();
					if(fullName[0].ToLower() == c.FirstName.ToLower() && fullName[1].ToLower() == c.MiddleName?.ToLower() && fullName[2].ToLower() == c.LastName.ToLower()){
						//return query here
						var flag = new Flag(ticketId, "OfacMatch", a.AdminId); // null = raised by the system, not an admin
						this.riskScore = "High"; 
					}
				}
			}
		}
		
		public void checkCountryTier(Admin a, Customer c, int ticketId){
			string CountryOfResidence = c.CountryOfResidence;
			string CountryOfCitizenship = c.CountryOfCitizenship;
			if( !(CountryOfResidence == "US" || CountryOfResidence == "MX") ){
				var flag = new Flag(ticketId, "ClosedJurisdiction", a.AdminId);
				this.riskScore = "Medium"; 
			}
		}
		
		public void checkIllicitSubstances(Admin a, Customer c, int ticketId){
			string occupation = c.Occupation;
			string employer = c.Employer;
			if((occupation.ToLower()).Contains("cannabis") || (occupation.ToLower()).Contains("marijuana") ||
			(occupation.ToLower()).Contains("dispensary") || (occupation.ToLower()).Contains("hemp") ||
			(occupation.ToLower()).Contains("thc") || (occupation.ToLower()).Contains("cbd") ){
                var flag = new Flag(ticketId, "ProhibitedIndustry" , a.AdminId);
				this.riskScore = "High"; 
			}
		}

        public VendorResponse Verify(Admin a, Customer c, int ticketId)
        {
            string nameMatch = "Match";
            string dateOfBirthMatch = "Match";
            string addressMatch = "Match";
            string taxIdMatch = "Match";
            var riskCodes = new List<string>();

            string deceasedPath = Path.Combine(Directory.GetCurrentDirectory(), "DeceasedSSNs.csv");
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
            Flag flag = new Flag(ticketId, "VerificationFailed", a.AdminId);
            return new VendorResponse(ticketId, nameMatch, dateOfBirthMatch, addressMatch, taxIdMatch, score, string.Join(",", riskCodes));
        }

	}
}