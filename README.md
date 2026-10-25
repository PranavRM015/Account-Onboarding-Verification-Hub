# Account Onboarding Verification Hub

**Author:** Pranav Madan

**Context:** Before a brokerage firm can open an account for a new customer, it is required to verify that the customer is who they claim to be, and that the customer does not fall outside of the firm's acceptance policies--such as being on a government sanctions list, being involved in a prohibited industry, or living in a country the firm does not accept customers from. When this review is slow or inconsistent, especially during periods where a large number of accounts are coming in at once, unverified accounts can stay open far longer than they should. An excellent way to deal with this is to keep all of the customer's information in one central place, and move each new account through a series of stages as a ticket, so that every account is checked against the same set of rules before it is approved. Additionally, every action taken on a ticket will be stored in a permanent record, so that the firm can show who reviewed each account, what they decided, and why.

**Goal (one sentence):** This application will verify the identity of incoming brokerage customers, flag those who do not meet the firm's acceptance rules for review, and keep a permanent record of every decision that is made.

**Overview:** The Account Onboarding Verification Hub will accept tickets from an Angular based form containing the customer's information--including the customer's name, date of birth, residential address, tax ID, occupation, employer, and country of residence. The API will validate the submission and save it to the database as a new ticket. The backend will consist of a validation pipeline containing stages dealing with format checking, identity verification (through a mocked third-party vendor), and screening the customer against the firm's rules. Any problems found along the way will be raised as flags. Apart from the 60-day limit described in the rules below, the system will not reject a customer on its own; instead, a reviewer will look at each flag and decide whether to clear it, restrict the account, or deny it. High risk tickets will need a second approval from a different reviewer before they can be approved.

**Tech Stack:**

| Aspect | Tool |
|---|---|
| Frontend | Angular, TypeScript, HTML, CSS |
| Backend | ASP.NET Core (C#) |
| Object Relational Mapper | Entity Framework Core |
| Database | Microsoft SQL Server |
| Authentication | ASP.NET Core cookie authentication (HttpOnly, 8 hour session) with a custom Admin table (PBKDF2-SHA512 password hashes) |
| Hosting | Local (MVP), Azure App Service and Azure SQL Database |
| Testing | xUnit (.NET), Jasmine/Karma (Angular) |
| CI/CD | GitHub Actions |

**Schema Design:**

Only constraints that exist in the SQL Server database are listed here. Rules that are only checked by the application are listed under Rules.

**Tables**

**Admin**

| Attribute | Type | Constraints |
|---|---|---|
| adminId | int | Not null, Identity(1,1), Primary key |
| username | nvarchar(100) | Not null, unique (referenced by Ticket.assignedTo, Ticket.firstApprovedBy and Decision.decidedBy) |
| passwordHash | nvarchar(200) | Not null |

A `system` admin exists only so that automatic rejections can be recorded as a decision. Its password hash can never match, so it cannot log in.

**Ticket**

| Attribute | Type | Constraints |
|---|---|---|
| ticketId | int | Not null, Identity(1,1), Primary key |
| customerId | int | Not null, Foreign key references Customer |
| jointCustomerId | int | Foreign key references Customer. Null for an Individual account |
| ticketStatus | varchar(20) | Not null, default Submitted. Can be one of the following: Submitted, Review, Approved, Rejected |
| accountType | varchar(15) | Not null, can be one of the following: Individual, Joint |
| riskRating | varchar(6) | Can be one of the following: Low, Medium, High. Null until scored |
| createdAt | datetime2 | Not null |
| verificationDeadline | datetime2 | Not null, must equal createdAt + 60 days |
| assignedTo | nvarchar(100) | Not null, Foreign key references Admin.username |
| firstApprovedBy | nvarchar(100) | Foreign key references Admin.username. Null until a High risk ticket gets its first approval |
| rowVersion | rowversion | Not null, generated automatically by SQL Server and changed on every update. Used to detect two reviewers changing the same ticket at once |

An Individual ticket must not have a jointCustomerId. A Joint ticket must have one, and it must be different from customerId.

**Customer**

| Attribute | Type | Constraints |
|---|---|---|
| customerId | int | Not null, Identity(1,1), Primary key |
| firstName | nvarchar(50) | Not null |
| middleName | nvarchar(50) | |
| lastName | nvarchar(50) | Not null |
| dateOfBirth | date | Not null |
| gender | varchar(6) | Not null, can be one of the following: Male, Female, Other |
| taxIdType | varchar(4) | Not null, can be one of the following: SSN, ITIN, EIN |
| taxIdHash | binary(32) | Not null, unique. Used to look up duplicates without storing the full number |
| taxIdLastFourDigits | char(4) | Not null, must be exactly 4 characters |
| residentialAddress | nvarchar(200) | Not null, must not be empty |
| countryOfResidence | char(2) | Not null |
| countryOfCitizenship | char(2) | Not null |
| occupation | nvarchar(100) | Not null |
| employer | nvarchar(100) | Not null |

**VendorResponse**

| Attribute | Type | Constraints |
|---|---|---|
| vendorResponseId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| nameMatch | varchar(7) | Not null, can be one of the following: Match, Partial, NoMatch |
| dateOfBirthMatch | varchar(7) | Not null, can be one of the following: Match, Partial, NoMatch |
| addressMatch | varchar(7) | Not null, can be one of the following: Match, Partial, NoMatch |
| taxIdMatch | varchar(7) | Not null, can be one of the following: Match, Partial, NoMatch |
| score | int | Not null, between 0 and 50 |
| riskCodes | nvarchar(200) | Not null |
| VRTimeStamp | datetime2 | Not null |

**Flag**

| Attribute | Type | Constraints |
|---|---|---|
| flagId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| violationType | varchar(30) | Not null, can be one of the following: VerificationFailed, OfacMatch, ProhibitedIndustry, ClosedJurisdiction |
| raisedBy | int | Foreign key references Admin.adminId |
| raisedAt | datetime2 | Not null |
| clearedBy | int | Foreign key references Admin.adminId |
| clearedAt | datetime2 | Null until the flag is cleared |
| clearedReason | nvarchar(500) | |

**Decision**

| Attribute | Type | Constraints |
|---|---|---|
| decisionId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, unique (one decision per ticket), Foreign key references Ticket |
| decisionType | varchar(20) | Not null, can be one of the following: Approve, Reject |
| decidedBy | nvarchar(100) | Not null, Foreign key references Admin.username |
| reason | nvarchar(1000) | Not null, must not be empty |
| decidedAt | datetime2 | Not null |

**AuditTrail**

| Attribute | Type | Constraints |
|---|---|---|
| recId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| customerId | int | Not null, Foreign key references Customer |
| prevStatus | varchar(20) | Not null, can be one of the following: Submitted, Review, Approved, Rejected |
| currStatus | varchar(20) | Not null, can be one of the following: Submitted, Review, Approved, Rejected |
| ATMessage | nvarchar(250) | Not null |
| reason | nvarchar(500) | Not null |
| dateTimeOccurred | datetime2 | Not null |

**Stages:**

Submitted, Review, Approved, Rejected

**Rules:**

- Every required field must be filled out and in the correct format (ex. SSN is 9 digits, the customer is at least 18 years old), otherwise the API returns an error and no ticket is created, so the submission is sent back before verification.
- If the vendor's verification score is below the threshold (scores run from 0 to 50 and anything under 50 fails), or the vendor returns a risk code such as a deceased SSN or a correctional facility address, a VerificationFailed flag will be raised.
- If the customer's name matches a name on the OFAC sanctions list, an OfacMatch flag will be raised, and a reviewer will compare the details to decide if it is the same person.
- Customers whose occupation or employer contains cannabis, marijuana, dispensary, hemp, thc or cbd will be flagged for review with a ProhibitedIndustry flag.
- Accounts based in the USA and Mexico are in the open tier and will be accepted. Everything else will be in the closed tier. This rule is applied to both the country of residence and the country of citizenship, so a ClosedJurisdiction flag is raised if either one is outside the USA and Mexico.
- A ticket cannot be approved until every flag on it has been cleared.
- High risk tickets will need a second approval from a different reviewer than the one who gave the first approval.
- A reason is required to clear a flag, approve a ticket or reject a ticket.
- The application never updates or deletes AuditTrail rows, and a ticket never gets a second decision.
- If a ticket is not resolved within 60 days, it is moved to Rejected. A stored procedure (`pr_expireReviewTickets`) does this: it sets the status to Rejected, records a decision by the system account with the reason "Not resolved within 60 days", and writes an audit row. The procedure is run by a background service in the API (`TicketExpiryService`), once when the API starts and then every hour. It is safe to run on more than one instance, because it only touches tickets that are still in Review and have no decision.

The risk rating is the highest level reached by any flag on the ticket. An OfacMatch or ProhibitedIndustry flag makes it High, a ClosedJurisdiction or VerificationFailed flag makes it Medium, and a ticket with no flags is Low.

**Security:**

- Admins sign in with a session cookie that is HttpOnly, SameSite=Lax, Secure outside Development, and expires after 8 hours. JWT is not used, because the Angular app and the API are served from the same origin, so an HttpOnly cookie keeps the login out of reach of page scripts and access can be revoked right away.
- Passwords are stored as salted PBKDF2-SHA512 hashes. Tax ID numbers are never stored, only a peppered hash and the last four digits.
- Creating an admin through the signup form needs a shared signup code (`Signup:Code`). If no code is configured, signup is disabled. The first admin of a new database can instead be created with `BootstrapAdmin:Username` and `BootstrapAdmin:Password`.
- Login and signup are limited to 10 requests per minute for each IP address, and the API answers 429 after that. `RateLimiting:Auth:PermitLimit` and `RateLimiting:Auth:WindowSeconds` change the limit.
- Every logged in admin has the same permissions. Roles are not part of the MVP.

**Testing:**

Backend: the `backend.Tests` project uses xUnit and has 72 tests.

- Vendor tests, which need no database: the country tiers, the cannabis keywords, the OFAC list, the deceased SSN and correctional facility checks, and the risk rating.
- Integration tests, which call the real API and use the database in the connection string (each test deletes the rows it created):
  - Every review scenario: a clean customer, an OFAC match, citizenship and residence combinations, and each cannabis keyword in the occupation and in the employer. Each one is accepted once (flags cleared, with a second reviewer for High risk) and rejected once.
  - The 60 day rejection: a ticket past its deadline, a ticket inside it, a ticket that already has a decision, running the procedure twice, and the timer rejecting a ticket on its own.
  - The signup code and the login and signup rate limit.
- Run them from the repository root with `dotnet test backend.Tests`. The migrations and `database/Procedures.sql` must be applied to the database first, and the API must not be running.

Frontend: Jasmine/Karma specs for the signup, login and input form components, and one integration spec (`frontend/src/app/integration/onboarding.integration.spec.ts`) that uses mocked HTTP. It types into the signup, login, input form, review and result pages for the same scenarios as the backend, and accepts or rejects each one. Run them from the `frontend` folder with `npx ng test --watch=false --browsers=ChromeHeadless`.

Not yet covered: tests that call the stored procedures directly.

**CI/CD Pipeline:**

The pipeline is a GitHub Actions workflow in `.github/workflows/ci-cd.yml`.

- On every pull request and every push to `main`, the test job starts a SQL Server container, builds the database from the EF Core migrations and `database/Procedures.sql`, and runs the xUnit and Karma tests.
- On a push to `main` where the tests passed, the deploy job builds the Angular app, copies it into `backend/wwwroot`, publishes the API and deploys it to Azure App Service. The deploy job uses the GitHub environment `production`, so it can be set to wait for an approval first.
- The deploy job needs the secret `AZURE_WEBAPP_PUBLISH_PROFILE` (the publish profile downloaded from the Web App) and the variable `AZURE_WEBAPP_NAME`. The pipeline does not change the production database. Schema changes have to be applied by hand (see below).

**Deploying to Azure:**

Production does not use `appsettings.Development.json`. Azure runs the app as Production, which ignores that file, and the settings in the Web App's configuration override `appsettings.json`. Nothing secret is stored in the repository.

Resources: an App Service plan and Web App on Windows with the .NET 10 runtime, and an Azure SQL logical server with a database. On the Web App, turn on Always On (otherwise the 60 day timer stops when the app goes idle) and HTTPS Only.

Application settings on the Web App:

| Setting | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | `Server=tcp:<server>.database.windows.net,1433;Database=<database>;Authentication=Active Directory Managed Identity;Encrypt=True;` |
| `TaxIdHashPepper` | A random value of at least 32 bytes. Different from the development value. Do not change it after customers exist, because the stored hashes would no longer match |
| `Signup__Code` | The shared signup code |
| `DataProtection__KeysPath` | `D:\home\DataProtection-Keys` |
| `BootstrapAdmin__Username` and `BootstrapAdmin__Password` | Only for the first deploy. Remove them after the first admin exists |

A pepper can be generated in PowerShell with `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`.

Database access uses the Web App's managed identity, so there is no password in the connection string:

1. Turn on the system assigned identity for the Web App.
2. Make an Entra ID user or group the admin of the SQL server, and allow Azure services through the SQL server firewall.
3. Connected to the database as that admin, run the following (the name is the Web App's name):

```sql
CREATE USER [<web-app-name>] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [<web-app-name>];
ALTER ROLE db_datawriter ADD MEMBER [<web-app-name>];
GRANT EXECUTE TO [<web-app-name>];
```

The schema is created once, as the SQL admin. Generate the script with `dotnet ef migrations script --idempotent --project backend -o schema.sql`, run it, and then run `database/Procedures.sql`. Both scripts can be run again safely, so run them again after a new migration.
