# Account Onboarding Verification Hub

**Author:** Pranav Madan, Sneh Patel, Michael Griswold, Daniyal Syed

**Context:** Before a brokerage firm can open an account for a new customer, it must verify that the customer is who they claim to be and that they do not fall outside the firm's acceptance policies, such as appearing on a government sanctions list, working in a prohibited industry, or living in a country the firm does not accept. When this review is slow or inconsistent, especially when many accounts arrive at once, unverified accounts can stay open far longer than they should. Keeping each customer's information in one place and moving every new account through the same stages as a ticket means each account is checked against the same rules before approval. Every action taken on a ticket is also stored permanently, so the firm can show who reviewed each account, what they decided and why.

**Goal:** Verify the identity of incoming brokerage customers, flag those who do not meet the firm's acceptance rules for review, and keep a permanent record of every decision that is made.

**Overview:** This application consists of four small ASP.NET Core services behind one Angular front end. An admin fills in a form with the customer's name, date of birth, residential address, tax ID, occupation, employer and country of residence. **Intake** saves the profile and asks **Risk** to screen it. Risk raises flags for problems it finds (failed verification, sanctions match, prohibited industry, closed jurisdiction) and rates the customer Low, Medium or High risk. **Identity** creates the customer's login, and **Review** opens a ticket holding the profile snapshot, flags and vendor response. Apart from the 60-day limit, the system never rejects a customer on its own: a reviewer looks at each flag and decides whether to clear it, approve or reject. A High risk ticket needs approval from two different reviewers.

**Tech Stack:**

| Aspect | Tool |
|---|---|
| Frontend | Angular, TypeScript, HTML, CSS |
| Backend | ASP.NET Core (C#, .NET 10), one project per service |
| Object Relational Mapper | Entity Framework Core |
| Database | Microsoft SQL Server (one database per stateful service) |
| Authentication | RS256-signed JWTs issued by Identity, validated by the other services through Identity's public JWKS |
| Service-to-service security | Shared internal API key (Intake to Identity) and forwarded admin JWT (Intake to Risk and Review) |
| Containers | Docker, Docker Compose |
| Testing | xUnit (unit tests per service and cross-service integration tests), Karma and Jasmine (Angular) |
| CI/CD | GitHub Actions: one workflow per service plus an integrated pipeline that gates on all of them |
| Hosting | Azure Container Apps, Azure Container Registry, Azure SQL Database (serverless), user-assigned managed identity |

**Authentication:**

- Identity is the only service that knows passwords. It checks a username and password, then issues a JWT signed with its private key (RS256). The token holds the role (`admin` or `customer`), the username (`name` claim), the account id and a token id (`jti`), and expires after 60 minutes by default.
- Passwords are stored as PBKDF2 hashes, and so are password recovery answers. Three wrong passwords lock the account for 15 minutes. Login, reset and signup endpoints are rate limited per IP address.
- Identity publishes its public keys at `GET /.well-known/jwks.json`. Intake, Risk and Review fetch those keys and validate the signature, issuer, audience and expiry themselves, so they never need Identity's private key or database. Keys are cached, so they keep validating tokens if Identity is briefly down.
- Roles are lowercase: `admin` and `customer`. All ticket, screening and admin-intake endpoints require `admin`.
- The browser only ever talks to public endpoints. The `/internal/**` endpoints are for other services only. Identity's `/internal/accounts` takes an `X-Internal-Key` header, while Risk and Review take the signed-in admin's token, which Intake forwards unchanged.
- Logging out records the token's `jti` in Identity's `RevokedTokens` table. Other services do not read that table, so a logged-out token still works elsewhere until it expires.

---

## Services at a glance

| Service | Local port | Docker address | Database | Role |
|---|---|---|---|---|
| Identity | 5290 | `http://identity:8080` | `VerificationHub_Identity` | Logins, passwords, tokens |
| Intake | 5168 | `http://intake:8080` | `VerificationHub_Intake` | Customer and admin profiles, onboarding orchestration |
| Risk | 5291 | `http://risk:8080` | none (stateless) | Screening rules, risk rating |
| Review | 5292 | `http://review:8080` | `VerificationHub_Review` | Tickets, flags, decisions, audit trail |

```
Angular (4200) ──► Identity   login, logout, password recovery
              ├──► Intake     signup, input form, pending customers
              └──► Review     ticket queue, clear flags, approve / reject

Intake ──► Identity   create the login        (X-Internal-Key)
Intake ──► Risk       screen the customer     (admin JWT)
Intake ──► Review     create / look up tickets (admin JWT)
Risk, Review ──► Identity   fetch public keys (JWKS)
```

No service reads another service's database, and no database has a foreign key into another service.

---

## Service: Identity

**Backend:**
Identity handles logins only. It does not store names, addresses or tax IDs; that stays in Intake.

- Creates a login when Intake asks it to (`POST /internal/accounts`), using the id Intake already assigned to the profile. Identity never generates ids.
- Checks username availability for Intake (`GET /internal/accounts/username-available`).
- Logs admins in and out (`POST /api/admins/login`, `/logout`) and logs customers in (`POST /api/users/login`, `GET /api/users/me`).
- Runs the two-step password recovery for both roles (`GET /recovery-question`, then `POST /reset-password`). Unknown usernames get a stable decoy question so the screen does not reveal which accounts exist.
- Applies the lock-out rule, signs and issues tokens, and publishes the public keys at `/.well-known/jwks.json`.
- Can create a first admin at startup from the `BootstrapAdmin__*` settings, and can apply its own migrations at startup (`Database__MigrateOnStartup`).

**Schema:** database `VerificationHub_Identity`

**AdminAccounts**

| Attribute | Type | Constraints |
|---|---|---|
| adminId | int | Not null, primary key, not auto-generated (the value comes from Intake) |
| username | nvarchar(100) | Not null, unique (`UX_AdminAccounts_Username`) |
| passwordHash | nvarchar(200) | Not null |
| passwordRecoveryQuestion | nvarchar(100) | Not null, must be one of four fixed questions |
| passwordRecoveryAnswer | nvarchar(100) | Not null, stored as a salted hash |
| failedLoginCount | int | Not null |
| lockoutEnd | datetime2 | Null unless the account is locked |

**CustomerAccounts** has the same columns, with `customerId` as the primary key and a unique `UX_CustomerAccounts_Username`.

**RevokedTokens**

| Attribute | Type | Constraints |
|---|---|---|
| jti | nvarchar(64) | Not null, primary key |
| expiresAt | datetime2 | Not null |

**Connects to other services:**
- **Intake calls Identity** to check a username, then to create the login after Intake has saved the profile. These calls carry the `X-Internal-Key` header and the same `id` Intake assigned.
- **Risk and Review call Identity** only to fetch its public keys. They never see credentials.
- **The browser calls Identity directly** for login, logout and password recovery.

---

## Service: Intake

**Backend:**
Intake owns the people. It saves customer and admin profiles and runs the onboarding steps in order, so a half-finished request never leaves an unreachable customer behind.

- **Customer self-signup** (`POST /api/users/signup`) and **admin signup** (`POST /api/admins/signup`): validate the profile, save it, then ask Identity to create the login. Confirmed rejections (bad input, duplicate username) delete the profile; uncertain outcomes (timeouts, 5xx) keep it and return `503` so the caller can retry.
- **Admin input form** (`POST /api/admins/inputForm`): validates the form, sends the profile to Risk, saves the customer, creates the login through Identity, then creates the ticket in Review. Duplicate tax IDs and usernames return a `409` that names the field that clashed.
- **Pending customers** (`GET /api/admins/pending-customers`, `GET .../{customerId}`, `POST .../{customerId}/submit`): lists self-registered customers who have no ticket yet, shows one, and submits one for screening and ticket creation.
- **Customer exists check** (`GET /api/admins/inputForm/customers/{customerId}/exists`): used by the form before a joint account is submitted.
- Tax IDs are never stored in full. Intake keeps the last four digits and a peppered hash, which is used only to find duplicates.
- Customer self-signup and admin signup are public but rate limited per IP.

**Schema:** database `VerificationHub_Intake`

**Customers**

| Attribute | Type | Constraints |
|---|---|---|
| customerId | int | Not null, identity, primary key |
| firstName | nvarchar(50) | Not null |
| middleName | nvarchar(50) | |
| lastName | nvarchar(50) | Not null |
| dateOfBirth | date | Not null |
| gender | varchar(6) | Not null, one of: Male, Female, Other |
| taxIdType | varchar(4) | Not null, one of: SSN, ITIN, EIN |
| taxIdHash | binary(32) | Not null, unique (`UX_Customers_TaxIdHash`) |
| taxIdLastFourDigits | char(4) | Not null, exactly 4 characters |
| residentialAddress | nvarchar(200) | Not null, must not be empty |
| countryOfResidence | char(2) | Not null |
| countryOfCitizenship | char(2) | Not null |
| occupation | nvarchar(100) | Not null |
| employer | nvarchar(100) | Not null |
| createdAt | datetime2 | Not null, set by the database on insert |
| username | nvarchar(100) | Not null, empty until Identity creates the login, unique when not empty (`UX_Customers_Username`, filtered on `[Username] <> ''`) |

The table also keeps credential columns (`passwordHash`, `passwordRecoveryQuestion`, `passwordRecoveryAnswer`, `failedLoginCount`, `lockoutEnd`) with defaults and no values. They are left over from the old shared schema and are not used. Identity owns the real credentials.

**Admins**

| Attribute | Type | Constraints |
|---|---|---|
| adminId | int | Not null, identity, primary key |
| firstName, middleName, lastName | nvarchar(50) | First and last name not null |
| dateOfBirth | date | Not null |
| gender | varchar(6) | Not null |
| ssnHash | binary(32) | Not null, unique (`UX_Admins_SSNHash`) |
| dateHired | datetime2 | Not null, set by the database on insert |
| isCitizen | bit | Not null |
| countryOfCitizenship | char(2) | Not null |
| username | nvarchar(100) | Not null, unique (`AK_Admins_Username`) |

**Connects to other services:**
- **Identity:** checks username availability and creates logins using `X-Internal-Key`. Intake also trusts Identity's public keys to validate admin tokens on its own endpoints.
- **Risk:** sends the customer's screening fields with the admin's JWT (`POST /internal/analyze`) and uses the returned rating, flags and vendor response.
- **Review:** creates a ticket (`POST /internal/tickets`), looks one up by customer, and lists customers who already have tickets, all with the admin's JWT. It asks Review rather than keeping its own copy of the ticket table.
- **The browser** calls Intake for signup, the input form and the pending-customer list.

---

## Service: Risk

**Backend:**
Risk is a stateless screening service. It has no database and saves nothing. One request in, one assessment out.

- `POST /internal/analyze` takes a customer's screening fields and returns a `riskRating` (Low, Medium, High), a list of `flags`, and a `vendorResponse` (name, date of birth, address and tax ID match results, a score from 0 to 50, and risk codes). The same input always gives the same output.
- The rules, run in this order:
  - A last-four-digits match against `DeceasedSSNs.csv` sets the tax ID match to `NoMatch`, lowers the score by 20 and adds the risk code `DeceasedSSN`.
  - A correctional-facility word in the address sets the address match to `Partial`, lowers the score by 10 and adds `CorrectionalAddress`.
  - Either of those adds the flag `VerificationFailed` and sets the rating to Medium.
  - A first, middle and last name match against `OFACNames.csv` adds `OfacMatch` and sets the rating to High.
  - A country of residence or citizenship other than the US or Mexico adds `ClosedJurisdiction` (Medium, unless already High).
  - Cannabis-related words in the occupation or employer add `ProhibitedIndustry` and set the rating to High.
- A High rating is a successful answer, not an error. If Risk is unreachable, Intake stops and returns `503`. It never assumes Low.
- `GET /health` is public and reports that the process is up with its screening files loaded. Both CSV files are read at startup, and a missing file stops the service rather than produce a misleading Low rating.

**Schema:** none. Risk has no database. Its request and response shapes are defined in `services/risk/Contracts` and documented in `services/risk/CONTRACT.md`.

**Connects to other services:**
- **Intake calls Risk** with the admin's JWT, a 15 second timeout and no side effects, so a retry after a failure is safe.
- **Risk calls Identity** only to download the public keys that validate the token. It rejects `customer` tokens with `403`.
- **Review does not call Risk.** Intake passes Risk's result to Review.

---

## Service: Review

**Backend:**
Review owns the tickets and the review workflow. It never writes profiles or credentials.

- `POST /internal/tickets` is called by Intake. It creates the ticket together with its flags and vendor response. Intake sends a `requestId` so a retried request returns the same ticket instead of creating a second one.
- The review queue and ticket actions (all admin only):
  - `GET /api/admins/tickets`: the queue, ordered by verification deadline.
  - `GET /api/admins/tickets/{ticketId}`: the full ticket with the customer snapshot, flags, vendor response and final decision.
  - `POST /api/admins/tickets/{ticketId}/flags/{flagId}/clear`: clears a flag with a required reason.
  - `POST /api/admins/tickets/{ticketId}/approve`: approves once every flag is cleared. High risk tickets need two approvals from two different admins. The first leaves the ticket in Review.
  - `POST /api/admins/tickets/{ticketId}/reject`: rejects with a reason, even when flags remain.
- Two reviewers cannot act on the same ticket at once. A row lock turns the second one away, and a row version column catches conflicting updates.
- A background job rejects any ticket still in Review after its 60-day deadline, recorded as a decision by `system`. It runs once at startup and then every hour. The work is done by SQL stored procedures in `services/review/database/Procedures.sql`: `pr_expireReviewTickets` finds the overdue tickets that have no decision, and for each one it calls `pr_ticketExpireReject`, `pr_decisionExpireReject` and `pr_auditTrailExpireReject` inside one transaction, so a failure on one ticket rolls back only that ticket. The procedures are not created by the migrations. `deploy/deploy-azure.ps1` installs them in Azure, and the integration pipeline installs them in its test database. The service's database user also needs `EXECUTE` on `pr_expireReviewTickets`, which the deploy script grants.
- Decisions and audit records can never be changed or deleted, which gives the firm its permanent record of who decided what and why.

**Schema:** database `VerificationHub_Review`

**Tickets**

| Attribute | Type | Constraints |
|---|---|---|
| ticketId | int | Not null, identity, primary key |
| customerId | int | Not null, positive (a plain id, no foreign key into Intake) |
| jointCustomerId | int | Null for an Individual account |
| ticketStatus | varchar(20) | Not null, default Submitted, one of: Submitted, Review, Approved, Rejected |
| accountType | varchar(15) | Not null, one of: Individual, Joint |
| riskRating | varchar(6) | Null until scored, one of: Low, Medium, High |
| createdAt | datetime2 | Not null |
| verificationDeadline | datetime2 | Not null, must equal createdAt + 60 days |
| assignedTo | nvarchar(100) | Not null, the Identity username (no foreign key) |
| firstApprovedBy | nvarchar(100) | Null until a High risk ticket gets its first approval |
| customerJson | nvarchar(max) | Snapshot of the customer's details at submission |
| jointCustomerName | nvarchar(151) | |
| requestId | uniqueidentifier | Not null, from Intake, makes retries safe |
| requestHash | nvarchar(64) | Not null |
| rowVersion | rowversion | Not null, changes on every update |

An Individual ticket must not have a `jointCustomerId`. A Joint ticket must have one, and it must differ from `customerId`.

**Flags**

| Attribute | Type | Constraints |
|---|---|---|
| flagId | int | Not null, identity, primary key |
| ticketId | int | Not null, foreign key references Tickets |
| violationType | varchar(30) | Not null, one of: VerificationFailed, OfacMatch, ProhibitedIndustry, ClosedJurisdiction |
| raisedBy | int | Identity admin id, null when raised by the system |
| raisedAt | datetime2 | Not null |
| clearedBy, clearedByName | int, nvarchar(100) | Set when cleared |
| clearedAt | datetime2 | Null until cleared |
| clearedReason | nvarchar(500) | Required when the flag is cleared |

**VendorResponses**

| Attribute | Type | Constraints |
|---|---|---|
| vendorResponseId | int | Not null, identity, primary key |
| ticketId | int | Not null, foreign key references Tickets |
| nameMatch, dateOfBirthMatch, addressMatch, taxIdMatch | varchar(7) | Not null, one of: Match, Partial, NoMatch |
| score | int | Not null, between 0 and 50 |
| riskCodes | nvarchar(200) | For example DeceasedSSN, CorrectionalAddress |
| vrTimeStamp | datetime2 | Not null |

**Decisions**

| Attribute | Type | Constraints |
|---|---|---|
| decisionId | int | Not null, identity, primary key |
| ticketId | int | Not null, foreign key references Tickets, one decision per ticket |
| decisionType | varchar(20) | Not null, one of: Approve, Reject |
| decidedBy | nvarchar(100) | Not null, Identity username |
| reason | nvarchar(1000) | Not null, must not be empty |
| decidedAt | datetime2 | Not null |

**AuditTrails**

| Attribute | Type | Constraints |
|---|---|---|
| recId | int | Not null, identity, primary key |
| ticketId | int | Not null, foreign key references Tickets |
| customerId | int | Not null, positive |
| prevStatus, currStatus | varchar(20) | Not null, one of the four ticket statuses |
| atMessage | nvarchar(250) | Not null |
| reason | nvarchar(500) | Not null |
| dateTimeOccurred | datetime2 | Not null |

**Connects to other services:**
- **Intake calls Review** to create tickets and look them up, forwarding the admin's JWT.
- **Review calls Identity** only to fetch its public keys. It reads the admin's id and name from the token, so assignment and flag attribution come from the token and not from the request body.
- **The browser calls Review** directly for the ticket queue and for approve, reject and clear-flag actions.
- **Review never calls Intake or Risk**, and it has no foreign keys into either database.

---

## Running the application

From the repository root, with Docker Desktop running and `.env` filled in from `.env.example`:

```powershell
docker compose up --build -d
```

This starts SQL Server and all four services. In a second terminal, start the front end:

```powershell
cd frontend
npx ng serve --proxy-config proxy.conf.json
```

Open `http://localhost:4200`. The dev proxy in `frontend/proxy.conf.json` sends `/api/admins/tickets` to Review, `/api/admins/signup`, `/inputForm`, `/pending-customers` and `/api/users/signup` to Intake, and the remaining `/api/admins` and `/api/users` paths to Identity.

The Identity, Intake and Review databases are created on first start: Identity and Review run their migrations, and Intake creates its tables.

### Trying the APIs in Swagger

Each service has a Swagger UI for calling its endpoints by hand. It is **on in the local Docker Compose stack and off everywhere else**: it only turns on when `Swagger__Enabled=true` or the app runs in Development, and Azure does not set either.

| Service | Swagger UI |
|---|---|
| Identity | `http://localhost:5290/swagger` |
| Intake | `http://localhost:5168/swagger` |
| Risk | `http://localhost:5291/swagger` |
| Review | `http://localhost:5292/swagger` |

1. In Identity's Swagger, call `POST /api/admins/login` and copy the `token` from the response.
2. On any service, click **Authorize** and paste the token (without the word `Bearer`). Each service's page needs its own paste.
3. Identity's `/internal/*` endpoints also need the shared key: Identity's Authorize dialog has an `X-Internal-Key` field, which takes `INTERNAL_API_KEY` from your `.env`.

---

## Testing

There is a test project for every service, plus one that tests them all working together. They live in `tests/`, one folder per project, so a service's own build never picks them up.

| Project | Kind | What it covers |
|---|---|---|
| `tests/Risk.Tests` | Unit | Every screening rule and every flag (`VerificationFailed`, `OfacMatch`, `ClosedJurisdiction`, `ProhibitedIndustry`). Every one of the 16 flag combinations is checked to produce exactly those flags and the rating Review expects. Also covers each entry in the OFAC and deceased-SSN files and request validation. |
| `tests/Review.Tests` | Unit | Ticket creation rules for every flag combination, including rating-versus-flag and vendor-finding consistency, joint accounts and age limits. Also the ticket, flag, decision, audit and vendor validators, and the rule that decisions and audit rows cannot change. |
| `tests/Identity.Tests` | Unit | Password hashing (including legacy bcrypt), the password policy, the 3-strikes lockout, recovery questions and decoys, the JWT service (claims, signature, expiry, key set, discovery document), the internal-key filter and account creation. |
| `tests/Intake.Tests` | Unit | Profile validation, tax ID hashing, the Identity, Risk and Review clients against stubbed responses, and the controllers with the other services stubbed. This includes the pending-customer list (it must return `503` rather than a wrong list when Review cannot answer) and that each flag Risk raises reaches Review unchanged. |
| `frontend` (Karma and Jasmine) | Unit | The Angular components and services. |
| `tests/Integration.Tests` | Integration | The real services and a real SQL Server, driven only over HTTP the way the Angular app does. |

The integration tests cover:
- **Every flag, end to end:** each flag and each of the 16 flag combinations, from entering a customer to a ticket in the review queue with the right flags, rating and vendor findings.
- **Clearing flags and approving:** open flags block approval, every flag must be cleared, High risk tickets need two different approvers, and a rejected or approved ticket cannot be decided again. Two reviewers acting at once produce exactly one winner.
- **Both entry paths:** the admin input form and the self-signup, pending-list and submit path.
- **Authentication:** login, lockout, password recovery, logout, and what happens with no token, a garbage token and a customer's token on admin endpoints.
- **The Risk to Review contract:** whatever Risk produces for every flag combination is accepted as is by Review, and a rating that disagrees with the flags is refused.
- **Ticket expiry:** the SQL procedures against a real database. Overdue tickets are rejected by `system` whatever flag they carry, tickets inside the deadline and tickets that already have a decision are left alone, and a second run changes nothing.

### Running the tests

Unit tests need nothing but the .NET 10 SDK:

```powershell
dotnet test tests/Risk.Tests
dotnet test tests/Review.Tests
dotnet test tests/Identity.Tests
dotnet test tests/Intake.Tests

cd frontend
npx ng test --watch=false --browsers=ChromeHeadlessCI
```

The integration tests need the full Compose stack. Use a throwaway stack, not the one holding data you care about, because the tests create admins, customers and tickets and age tickets in the Review database:

```powershell
# 1. Start the stack. The CI override relaxes the per-IP rate limit, which the tests would otherwise hit.
docker compose -f docker-compose.yml -f docker-compose.ci.yml up -d --build

# 2. Install the Review ticket-expiry procedures (Review's migrations have created its tables by now).
docker compose cp services/review/database/Procedures.sql sqlserver:/tmp/Procedures.sql
docker compose exec -T sqlserver sh -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -I -d VerificationHub_Review -i /tmp/Procedures.sql'

# 3. Point the tests at the Review database and run them.
$env:REVIEW_DB_CONNECTION = "Server=127.0.0.1,1433;Database=VerificationHub_Review;User Id=sa;Password=<your SQL_SA_PASSWORD>;TrustServerCertificate=True;Encrypt=False"
dotnet test tests/Integration.Tests

# 4. Throw the stack away.
docker compose -f docker-compose.yml -f docker-compose.ci.yml down -v
```

The service addresses default to the Compose ports (5290, 5168, 5291 and 5292). To aim the tests at a stack on other ports, set `IDENTITY_URL`, `INTAKE_URL`, `RISK_URL` and `REVIEW_URL`. Use `127.0.0.1` rather than `localhost` in the database connection string, because `localhost` can resolve to an IPv6 address that Docker does not publish.

---

## CI/CD

Pipelines are GitHub Actions workflows in `.github/workflows/`. Every service has its own pipeline, and one integrated pipeline calls all of them and only goes on if every one passes.

```
identity ─┐
risk     ─┤
review   ─┼─► integration ─► deploy (main only)
intake   ─┤
frontend ─┘
```

| Workflow | Runs on | What it does |
|---|---|---|
| `identity.yml`, `risk.yml`, `review.yml`, `intake.yml` | Pull requests that touch that service or its tests, manual start, and when called by `ci-cd.yml` | Runs the service's unit tests, then builds its Docker image (not pushed) to catch Dockerfile breakage. |
| `frontend.yml` | Pull requests that touch `frontend/`, manual start, and when called by `ci-cd.yml` | Runs the Angular unit tests headless, makes a production build, and builds the Docker image (not pushed). |
| `ci-cd.yml` | Pushes to `main`, pull requests into `main`, and manual start | Calls the five workflows above in parallel. Then, only if all five pass, runs the integration tests. Then, only on `main`, deploys. |

**The gate is real.** The five service workflows are called as reusable workflows (`workflow_call`), so `integration` needs all five, and `deploy` needs `integration`. A failure anywhere stops everything after it. On a pull request both the service's own workflow and the integrated one run, which means the unit tests run twice. In the repository's branch protection rules, set the integrated pipeline's jobs (and the per-service ones) as required checks so nothing merges around the gate.

**Integration job.** It creates a throwaway `.env` with freshly generated secrets (SQL password, internal API key, tax ID pepper and an RSA signing key; all masked in logs and gone when the job ends), starts the full `docker-compose` stack with the CI override, waits until every service reports healthy, installs the Review procedures into the Review database, runs `tests/Integration.Tests`, prints the service logs if anything failed, and always tears the stack down. It needs no repository secrets.

**Deploy job.** It only runs for a push (or manual run) on `main` and is **off until you turn it on**: set the repository variable `DEPLOY_ENABLED` to `true`. It also needs a GitHub environment named `dev` and these repository variables, which let the job sign in to Azure with OpenID Connect so no password or key is stored in GitHub:

| Variable | Meaning |
|---|---|
| `DEPLOY_ENABLED` | `true` to allow the deploy job to run |
| `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` | An Entra app registration with a federated credential for this repository and the `dev` environment. It needs permission to push to the registry (AcrPush) and to update the Container Apps. |
| `ACR_NAME`, `AZURE_RESOURCE_GROUP` | Optional; default to `aovhacrprm` and `rg-verificationhub-dev-cus-01` |

The job builds all five images, tags each with the commit SHA and with `dev`, pushes them, points each Container App at the commit-tagged image with `az containerapp update`, and then smoke-tests that Identity's key endpoint and the website answer. The SHA tag means every revision is traceable to a commit and always pulls a new image.

The pipeline only **releases new images**. It does not create secrets, database users, the SQL procedures or the apps themselves. That one-time setup is `deploy/deploy-azure.ps1`, described below.

---

## Hosting on Azure

The dev environment runs in one resource group, `rg-verificationhub-dev-cus-01`, in Central US.

```
Browser ──► ca-hub-web (nginx + Angular) ──► /api/*  ──► ca-hub-identity ─┐
                                                       ├► ca-hub-intake ───┤──► Azure SQL
                                                       └► ca-hub-review ───┘    (3 serverless databases)
ca-hub-intake ──► ca-hub-identity, ca-hub-risk (internal only), ca-hub-review
```

| Resource | Name | Notes |
|---|---|---|
| Container Apps environment | one per resource group | The deploy script finds it automatically and stops if there is more than one. |
| Container Apps | `ca-hub-identity`, `ca-hub-intake`, `ca-hub-review`, `ca-hub-risk`, `ca-hub-web` | One replica each. Identity, Intake, Review and web have public ingress. **Risk has internal ingress only**, so it cannot be called from the internet. |
| Container registry | `aovhacrprm` | Images are `hub/identity`, `hub/intake`, `hub/risk`, `hub/review` and `hub/web`, tagged `dev` (and by commit SHA when released by the pipeline). |
| SQL server | `srv-verificationhub-dev-cus` | Three General Purpose serverless databases: `db-verificationhub-identity-dev-cus`, `db-verificationhub-intake-dev-cus` and `db-verificationhub-review-dev-cus`. |
| Managed identity | `id-hub-dev` | One user-assigned identity shared by the apps. It pulls images from the registry (AcrPull) and signs in to the databases with Microsoft Entra authentication, so **no database password exists**. |

**How the pieces connect**
- **The web app** is nginx serving the compiled Angular app. It is the only address users need. It forwards `/api` calls to the three public APIs by path, using the same routing as `frontend/proxy.conf.json`: `/api/admins/tickets` goes to Review, signup, input form and pending customers go to Intake, and everything else under `/api` goes to Identity. The three upstream addresses are set as app settings (`IDENTITY_UPSTREAM`, `INTAKE_UPSTREAM`, `REVIEW_UPSTREAM`).
- **Service to service** traffic stays inside the environment as plain `http://ca-hub-<name>`, which is why each app is set to allow insecure internal calls. Identity's key address, Risk's address and Review's address are all set this way.
- **Secrets** (`INTERNAL_API_KEY`, the RSA signing key `JWT_PRIVATE_KEY_PEM`, `TAX_ID_PEPPER` and the connection strings) are stored as Container App secrets and referenced by the apps. The deploy script reads them from the repository-root `.env` and never prints them.
- **Swagger is off** in Azure. The apps only serve it when `Swagger__Enabled` is set.

The current dev site is `https://ca-hub-web.calmbush-b4a72100.centralus.azurecontainerapps.io`. To look up any app's address:

```powershell
az containerapp show -n ca-hub-web -g rg-verificationhub-dev-cus-01 --query properties.configuration.ingress.fqdn -o tsv
```

### Setting up or updating the environment

`deploy/deploy-azure.ps1` builds the whole environment from images that are already in the registry. It is safe to run again, because every step checks first and skips what already exists. Run it from Windows PowerShell, signed in with `az login` as the SQL server's Microsoft Entra admin, with `INTERNAL_API_KEY`, `JWT_PRIVATE_KEY_PEM` and `TAX_ID_PEPPER` set in the repository-root `.env`:

```powershell
.\deploy\deploy-azure.ps1
```

| Step | What it does |
|---|---|
| 1 | Checks all five images exist in the registry with the `dev` tag. |
| 2 | Finds the Container Apps environment. |
| 3 | Creates the `id-hub-dev` managed identity and gives it AcrPull on the registry. |
| 4 | Opens the SQL firewall for your IP, creates the identity's database user and gives it `db_datareader`, `db_datawriter` and `db_ddladmin` on each database, installs the Review ticket-expiry procedures, and grants `EXECUTE` on `pr_expireReviewTickets`. |
| 5 | Creates or updates the five Container Apps in dependency order: Identity, Risk, Review, Intake, web. |
| 6 | Prints each app's address and state. |

Useful switches: `-SkipSql` skips step 4 (including the procedures), `-RotateInternalKey` generates a new internal API key for Azure, and the resource names are parameters so the same script can target another environment.

**To release one service by hand**, push a new image and point the app at it. Use a **new tag** each time, because updating to an image string the app already has may not start a new revision:

```powershell
az acr login --name aovhacrprm
docker build -t aovhacrprm.azurecr.io/hub/intake:<new-tag> services/intake
docker push aovhacrprm.azurecr.io/hub/intake:<new-tag>
az containerapp update -n ca-hub-intake -g rg-verificationhub-dev-cus-01 --image aovhacrprm.azurecr.io/hub/intake:<new-tag>
```

`deploy-azure.ps1` points every app at the `:dev` tag. If you released something under another tag, also push it as `dev` (`docker tag … :dev`, `docker push … :dev`) before running the script, or the script will move that app back to the old image.

### Troubleshooting

- **A page takes a minute and then fails** ("Could not load the waiting customers"). The page shows the same message for any failed request, so find which step is slow. Time each request with a signed-in token (the public address of each service answers the same routes), then read the logs for the slow one:
  ```powershell
  az containerapp logs show -n ca-hub-intake -g rg-verificationhub-dev-cus-01 --tail 100
  ```
  Two causes have happened so far:
  - **A paused database.** Serverless databases pause when idle and take a minute or more to wake, which is longer than Intake's 15-second wait for Review, so Intake returned `503`. Auto-pause is turned off on the dev databases with `az sql db update … --auto-pause-delay -1`; confirm each one with `az sql db show … --query autoPauseDelay` (it should say `-1`). The trade-off is that each database is billed for its minimum compute all the time. Set it back with `--auto-pause-delay 60` (minutes) if the environment will sit unused.
  - **Intake could not fetch Identity's signing keys.** Intake used to find them through Identity's discovery document, which built the key address from the incoming request and was unreachable inside Azure's internal network, so every signed-in call hung for 60 seconds and then returned `401`. Intake now fetches the keys directly from `Identity:BaseUrl` plus `/.well-known/jwks.json`, like Risk and Review. Its log shows a `GET http://ca-hub-identity/.well-known/jwks.json` that answers in milliseconds.
- **A database will not connect from your machine.** `sqlcmd` waits only 8 seconds to log in, so use `-l 120`, and note that `-G` on its own tries Windows integrated sign-in. The sign-in code in `deploy-azure.ps1` (an `az` access token on a `System.Data.SqlClient` connection with a 120-second timeout) works reliably.
- **`az` or `docker build` fails with a certificate error.** Behind a network that inspects HTTPS traffic, add that network's root certificate. `Dockerfile.local` and `docker-compose.local.yml` (both ignored by git) do this for the image builds.
- **The Review log shows SQL error 229.** The service's database user lacks `EXECUTE` on `pr_expireReviewTickets`. Run `deploy-azure.ps1` without `-SkipSql`, which grants it.
