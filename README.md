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
- A background job rejects any ticket still in Review after its 60-day deadline, recorded as a decision by `system`. It runs once at startup and then every hour.
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
