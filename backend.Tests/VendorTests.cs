using Admins;
using Customers;
using MockVendor;

namespace backend.Tests
{
    public class VendorTests
    {
        private static Admin AnAdmin()
        {
            return new Admin("tester", "x") { AdminId = 1 };
        }

        private static Customer ACustomer()
        {
            return new Customer
            {
                CustomerId = 1,
                FirstName = "Sam",
                MiddleName = "Lee",
                LastName = "Taylor",
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = "Other",
                TaxIdType = "SSN",
                TaxIdLastFourDigits = "1234",
                ResidentialAddress = "12 Main Street",
                CountryOfResidence = "US",
                CountryOfCitizenship = "US",
                Occupation = "Teacher",
                Employer = "City School",
            };
        }

        private static Customer AnOfacCustomer()
        {
            var customer = ACustomer();
            customer.FirstName = "James";
            customer.MiddleName = "Robert";
            customer.LastName = "Anderson";
            return customer;
        }

        [Theory]
        [InlineData("US")]
        [InlineData("MX")]
        public void OpenTierCountriesAreNotFlagged(string country)
        {
            var customer = ACustomer();
            customer.CountryOfResidence = country;
            var vendor = new Vendor(customer);
            Assert.Null(vendor.CheckCountryTier(AnAdmin(), customer, 1));
        }

        [Theory]
        [InlineData("CA")]
        [InlineData("IN")]
        public void ClosedTierCountriesRaiseClosedJurisdiction(string country)
        {
            var customer = ACustomer();
            customer.CountryOfResidence = country;
            var vendor = new Vendor(customer);
            var flag = vendor.CheckCountryTier(AnAdmin(), customer, 1);
            Assert.NotNull(flag);
            Assert.Equal("ClosedJurisdiction", flag!.ViolationType);
        }

        [Theory]
        [InlineData("Cannabis Farmer", "City School")]
        [InlineData("Teacher", "Green Dispensary LLC")]
        [InlineData("Hemp Grower", "City School")]
        public void CannabisOccupationOrEmployerRaisesProhibitedIndustry(string occupation, string employer)
        {
            var customer = ACustomer();
            customer.Occupation = occupation;
            customer.Employer = employer;
            var vendor = new Vendor(customer);
            var flag = vendor.CheckIllicitSubstances(AnAdmin(), customer, 1);
            Assert.NotNull(flag);
            Assert.Equal("ProhibitedIndustry", flag!.ViolationType);
        }

        [Fact]
        public void OrdinaryJobIsNotFlaggedAsProhibitedIndustry()
        {
            var customer = ACustomer();
            var vendor = new Vendor(customer);
            Assert.Null(vendor.CheckIllicitSubstances(AnAdmin(), customer, 1));
        }

        [Fact]
        public void NameOnTheOfacListRaisesOfacMatch()
        {
            var customer = AnOfacCustomer();
            var vendor = new Vendor(customer);
            var flag = vendor.CheckOFACVerification(AnAdmin(), customer, 1);
            Assert.NotNull(flag);
            Assert.Equal("OfacMatch", flag!.ViolationType);
        }

        [Fact]
        public void OfacMatchIgnoresCase()
        {
            var customer = ACustomer();
            customer.FirstName = "JAMES";
            customer.MiddleName = "robert";
            customer.LastName = "anderson";
            var vendor = new Vendor(customer);
            Assert.NotNull(vendor.CheckOFACVerification(AnAdmin(), customer, 1));
        }

        [Fact]
        public void OfacRowWithoutMiddleNameMatchesAnyMiddleName()
        {
            var customer = ACustomer();
            customer.FirstName = "Maria";
            customer.MiddleName = "Elena";
            customer.LastName = "Gonzalez";
            var vendor = new Vendor(customer);
            Assert.NotNull(vendor.CheckOFACVerification(AnAdmin(), customer, 1));
        }

        [Fact]
        public void NameNotOnTheOfacListIsNotFlagged()
        {
            var customer = ACustomer();
            var vendor = new Vendor(customer);
            Assert.Null(vendor.CheckOFACVerification(AnAdmin(), customer, 1));
        }

        [Fact]
        public void OfacHeaderRowIsNotTreatedAsAName()
        {
            var customer = ACustomer();
            customer.FirstName = "First Name";
            customer.MiddleName = "Middle Name";
            customer.LastName = "Last Name";
            var vendor = new Vendor(customer);
            Assert.Null(vendor.CheckOFACVerification(AnAdmin(), customer, 1));
        }

        [Fact]
        public void CleanCustomerPassesVerification()
        {
            var customer = ACustomer();
            var vendor = new Vendor(customer);
            var response = vendor.Verify(customer, 1);
            Assert.Equal(50, response.Score);
            Assert.Equal(string.Empty, response.RiskCodes);
            Assert.Null(vendor.CheckVerification(AnAdmin(), response, 1));
        }

        [Fact]
        public void DeceasedSsnRaisesVerificationFailed()
        {
            var customer = ACustomer();
            customer.TaxIdLastFourDigits = "0000";
            var vendor = new Vendor(customer);
            var response = vendor.Verify(customer, 1);
            Assert.Contains("DeceasedSSN", response.RiskCodes);
            Assert.Equal(30, response.Score);
            var flag = vendor.CheckVerification(AnAdmin(), response, 1);
            Assert.NotNull(flag);
            Assert.Equal("VerificationFailed", flag!.ViolationType);
        }

        [Fact]
        public void CorrectionalAddressRaisesVerificationFailed()
        {
            var customer = ACustomer();
            customer.ResidentialAddress = "1 State Prison Road";
            var vendor = new Vendor(customer);
            var response = vendor.Verify(customer, 1);
            Assert.Contains("CorrectionalAddress", response.RiskCodes);
            Assert.Equal(40, response.Score);
            Assert.NotNull(vendor.CheckVerification(AnAdmin(), response, 1));
        }

        [Fact]
        public void NoFlagsGivesLowRisk()
        {
            var vendor = new Vendor(ACustomer());
            Assert.Equal("Low", vendor.FinalRiskRating());
        }

        [Fact]
        public void ClosedJurisdictionAloneGivesMediumRisk()
        {
            var customer = ACustomer();
            customer.CountryOfResidence = "IN";
            var vendor = new Vendor(customer);
            vendor.CheckCountryTier(AnAdmin(), customer, 1);
            Assert.Equal("Medium", vendor.FinalRiskRating());
        }

        [Fact]
        public void OfacMatchGivesHighRisk()
        {
            var customer = AnOfacCustomer();
            var vendor = new Vendor(customer);
            vendor.CheckOFACVerification(AnAdmin(), customer, 1);
            Assert.Equal("High", vendor.FinalRiskRating());
        }

        [Fact]
        public void HighestRatingWinsWhateverTheOrder()
        {
            var customer = AnOfacCustomer();
            customer.CountryOfResidence = "IN";
            var vendor = new Vendor(customer);
            vendor.CheckOFACVerification(AnAdmin(), customer, 1);
            vendor.CheckCountryTier(AnAdmin(), customer, 1);
            Assert.Equal("High", vendor.FinalRiskRating());
        }
    }
}
