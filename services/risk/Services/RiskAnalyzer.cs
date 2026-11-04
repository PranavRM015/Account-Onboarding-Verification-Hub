using Risk.Api.Contracts;

namespace Risk.Api.Services
{
    // Extracted from the monolith's Vendor.cs. File data is shared, but every assessment uses its own local state.
    public class RiskAnalyzer
    {
        private readonly string[][] _ofacNames;
        private readonly HashSet<string> _deceasedLastFour;
        private static readonly string[] IndustryKeywords = { "cannabis", "marijuana", "dispensary", "hemp", "thc", "cbd" };
        private static readonly string[] FacilityKeywords = { "correctional", "penitentiary", "state prison", "detention center", "jail" };

        public RiskAnalyzer() : this(AppContext.BaseDirectory) { }

        public RiskAnalyzer(string dataDirectory)
        {
            _ofacNames = File.ReadLines(Path.Combine(dataDirectory, "OFACNames.csv"))
                .Skip(1).Select(line => line.Split(',')).Where(parts => parts.Length >= 3)
                .Select(parts => parts.Select(part => part.Trim()).ToArray()).ToArray();
            _deceasedLastFour = File.ReadLines(Path.Combine(dataDirectory, "DeceasedSSNs.csv"))
                .Skip(1).Select(line => line.Trim()).ToHashSet();
        }

        public AnalyzeResponse Analyze(AnalyzeRequest customer)
        {
            var flags = new List<string>();
            var riskCodes = new List<string>();
            string addressMatch = "Match";
            string taxIdMatch = "Match";
            int score = 50;
            string rating = "Low";

            if (_deceasedLastFour.Contains(customer.TaxIdLastFourDigits))
            {
                taxIdMatch = "NoMatch";
                riskCodes.Add("DeceasedSSN");
                score -= 20;
            }
            if (ContainsKeyword(customer.ResidentialAddress, FacilityKeywords))
            {
                addressMatch = "Partial";
                riskCodes.Add("CorrectionalAddress");
                score -= 10;
            }
            if (score < 50 || riskCodes.Count > 0)
            {
                flags.Add("VerificationFailed");
                rating = "Medium";
            }

            string middle = customer.MiddleName?.Trim() ?? string.Empty;
            bool ofacMatch = _ofacNames.Any(name =>
                string.Equals(name[0], customer.FirstName.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(name[2], customer.LastName.Trim(), StringComparison.OrdinalIgnoreCase)
                && (name[1].Length == 0 || middle.Length == 0
                    || string.Equals(name[1], middle, StringComparison.OrdinalIgnoreCase)));
            if (ofacMatch)
            {
                flags.Add("OfacMatch");
                rating = "High";
            }
            if (!OpenCountry(customer.CountryOfResidence) || !OpenCountry(customer.CountryOfCitizenship))
            {
                flags.Add("ClosedJurisdiction");
                if (rating != "High") { rating = "Medium"; }
            }
            if (ContainsKeyword(customer.Occupation, IndustryKeywords) || ContainsKeyword(customer.Employer, IndustryKeywords))
            {
                flags.Add("ProhibitedIndustry");
                rating = "High";
            }

            // The mock vendor always reports matching name and birth date; OFAC is a separate flag check.
            var vendor = new VendorResult("Match", "Match", addressMatch, taxIdMatch, Math.Max(0, score), string.Join(",", riskCodes));
            return new AnalyzeResponse(rating, flags.ToArray(), vendor);
        }

        private static bool OpenCountry(string country) => country == "US" || country == "MX";

        private static bool ContainsKeyword(string value, string[] keywords) =>
            keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
