using System.Net;
using System.Net.Http.Json;

namespace backend.Tests
{
    // The new signup fields: admin profile details, customer self-signup, and the customer an admin signs up on someone's behalf.
    public class AccountSignupTests : IClassFixture<HubFixture>
    {
        private readonly HubFixture _hub;

        public AccountSignupTests(HubFixture hub)
        {
            _hub = hub;
        }

        private string Name(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

        private async Task<HttpStatusCode> AdminSignup(object body) =>
            (await _hub.NewClient().PostAsJsonAsync("/api/admins/signup", body)).StatusCode;

        private async Task<HttpStatusCode> UserSignup(object body) =>
            (await _hub.NewClient().PostAsJsonAsync("/api/users/signup", body)).StatusCode;

        [Fact]
        public async Task AdminDateHiredIsSetByTheSystem()
        {
            var username = await _hub.NewAdmin("hired");

            var hired = _hub.Scalar<DateTime>("SELECT DateHired FROM Admins WHERE Username = @u", ("@u", username));

            Assert.InRange((DateTime.UtcNow - hired).TotalMinutes, -1, 5);
        }

        [Fact]
        public async Task AdminSsnIsStoredAsAHashAndMustBeUnique()
        {
            var username = Name("ssn");
            _hub.TrackAdmin(username);
            var ssn = _hub.NewTaxId();
            await _hub.NewClient().PostAsJsonAsync("/api/admins/signup", new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = new DateTime(1990, 1, 1),
                gender = "Male", ssn, isCitizen = true, countryOfCitizenship = "US",
                passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = HubFixture.Answer,
            });
            var other = Name("ssn");
            _hub.TrackAdmin(other);

            var duplicate = await _hub.NewClient().PostAsJsonAsync("/api/admins/signup", new
            {
                username = other, password = HubFixture.Password, firstName = "C", lastName = "D", dateOfBirth = new DateTime(1991, 1, 1),
                gender = "Female", ssn, isCitizen = false, countryOfCitizenship = "MX",
                passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = HubFixture.Answer,
            });

            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            Assert.Equal(32, _hub.Scalar<int>("SELECT DATALENGTH(SSNHash) FROM Admins WHERE Username = @u", ("@u", username)));
        }

        [Theory]
        [InlineData("Robot")]
        [InlineData("")]
        public async Task AdminGenderMustBeMaleFemaleOrOther(string gender)
        {
            var username = Name("gen");
            var status = await AdminSignup(new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = new DateTime(1990, 1, 1),
                gender, ssn = _hub.NewTaxId(), isCitizen = true, countryOfCitizenship = "US",
                passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = HubFixture.Answer,
            });

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Fact]
        public async Task AdminCountryOfCitizenshipMustBeARealCountryCode()
        {
            var username = Name("cty");
            var status = await AdminSignup(new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = new DateTime(1990, 1, 1),
                gender = "Male", ssn = _hub.NewTaxId(), isCitizen = true, countryOfCitizenship = "ZZ",
                passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = HubFixture.Answer,
            });

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Fact]
        public async Task AdminRecoveryQuestionMustBeOneOfTheOfferedQuestions()
        {
            var username = Name("qst");
            var status = await AdminSignup(new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = new DateTime(1990, 1, 1),
                gender = "Male", ssn = _hub.NewTaxId(), isCitizen = true, countryOfCitizenship = "US",
                passwordRecoveryQuestion = "What is your favorite color?", passwordRecoveryAnswer = HubFixture.Answer,
            });

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Fact]
        public async Task AdminRecoveryAnswerIsRequired()
        {
            var username = Name("ans");
            var status = await AdminSignup(new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = new DateTime(1990, 1, 1),
                gender = "Male", ssn = _hub.NewTaxId(), isCitizen = true, countryOfCitizenship = "US",
                passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = " ",
            });

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Fact]
        public async Task AdminMustBeAnAdult()
        {
            var username = Name("kid");
            var status = await AdminSignup(new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = DateTime.UtcNow.Date.AddYears(-10),
                gender = "Male", ssn = _hub.NewTaxId(), isCitizen = true, countryOfCitizenship = "US",
                passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = HubFixture.Answer,
            });

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Fact]
        public async Task AdminPasswordMustMeetThePolicy()
        {
            var username = Name("pwd");

            Assert.Equal(HttpStatusCode.BadRequest, await AdminSignup(_hub.AdminBody(username, "weak")));
        }

        [Fact]
        public async Task UserSelfSignupCreatesACustomerWhoCanLogIn()
        {
            var username = await _hub.NewUser("self");

            var login = await _hub.NewClient().PostAsJsonAsync("/api/users/login", new { username, password = HubFixture.Password });

            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Equal(1, _hub.Scalar<int>("SELECT COUNT(*) FROM Customers WHERE Username = @u", ("@u", username)));
        }

        [Fact]
        public async Task UserSignupWithATakenUsernameIsAConflict()
        {
            var username = await _hub.NewUser("dup");

            Assert.Equal(HttpStatusCode.Conflict, await UserSignup(_hub.UserBody(username)));
        }

        [Fact]
        public async Task UserSignupWithAnExistingTaxIdIsAConflict()
        {
            var first = Name("tax");
            _hub.TrackCustomer(first);
            var taxId = _hub.NewTaxId();
            object Body(string username) => new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = new DateTime(1985, 4, 12),
                gender = "Other", taxIdType = "SSN", taxIdNumber = taxId, residentialAddress = "12 Main Street",
                countryOfResidence = "US", countryOfCitizenship = "US", occupation = "Teacher", employer = "School",
                passwordRecoveryQuestion = HubFixture.Question, passwordRecoveryAnswer = HubFixture.Answer,
            };
            Assert.Equal(HttpStatusCode.OK, await UserSignup(Body(first)));

            var second = Name("tax");
            _hub.TrackCustomer(second);

            Assert.Equal(HttpStatusCode.Conflict, await UserSignup(Body(second)));
        }

        [Fact]
        public async Task UserPasswordMustMeetThePolicy()
        {
            Assert.Equal(HttpStatusCode.BadRequest, await UserSignup(_hub.UserBody(Name("weak"), "weak")));
        }

        [Fact]
        public async Task UserRecoveryQuestionMustBeOneOfTheOfferedQuestions()
        {
            var username = Name("uq");
            _hub.TrackCustomer(username);
            var status = await UserSignup(new
            {
                username, password = HubFixture.Password, firstName = "A", lastName = "B", dateOfBirth = new DateTime(1985, 4, 12),
                gender = "Other", taxIdType = "SSN", taxIdNumber = _hub.NewTaxId(), residentialAddress = "12 Main Street",
                countryOfResidence = "US", countryOfCitizenship = "US", occupation = "Teacher", employer = "School",
                passwordRecoveryQuestion = "Anything goes?", passwordRecoveryAnswer = HubFixture.Answer,
            });

            Assert.Equal(HttpStatusCode.BadRequest, status);
        }

        [Fact]
        public async Task AnAdminCanSignAUserUpThroughTheInputForm()
        {
            var form = new CustomerForm();

            var ticket = await _hub.Submit(form);

            Assert.True(ticket.TicketId > 0);
            var login = await _hub.NewClient().PostAsJsonAsync("/api/users/login", new { username = form.Username, password = HubFixture.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }

        [Fact]
        public async Task TheInputFormRejectsATakenUsername()
        {
            var taken = await _hub.NewUser("taken");
            var form = new CustomerForm { Username = taken, TaxIdNumber = _hub.NewTaxId() };

            var response = await _hub.One.PostAsJsonAsync("/api/admins/inputForm", form);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }
    }
}
