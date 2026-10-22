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
| Authentication | ASP.NET Core cookie authentication with a custom Admin table (PBKDF2-SHA512 password hashes) |
| Hosting | Local (MVP), Azure (post-MVP) |
| Testing | xUnit (.NET), Jasmine/Karma (Angular) |

**Schema Design:**

**Tables**

**Admin**

| Attribute | Type | Constraints |
|---|---|---|
| adminId | int | Not null, Identity(1,1), Primary key |
| username | nvarchar(100) | Not null, unique (alternate key; referenced by Ticket.assignedTo and Decision.decidedBy) |
| passwordHash | nvarchar(200) | Not null, salted PBKDF2 hash stored as base64(salt).base64(hash); the plain password is never stored |

A `system` admin exists only so that automatic rejections can be recorded as a decision. Its password hash can never match, so it cannot log in.

**Ticket**

| Attribute | Type | Constraints |
|---|---|---|
| ticketId | int | Not null, Identity(1,1), Primary key |
| customerId | int | Not null, Foreign key references Customer |
| ticketStatus | varchar(20) | Not null, default Submitted. Can be one of the following: Submitted, Review, Approved, Rejected |
| accountType | varchar(15) | Not null, can be one of the following: Individual, Joint |
| riskRating | varchar(6) | Can be one of the following: Low, Medium, High. Null until scored |
| createdAt | datetime2 | Not null |
| verificationDeadline | datetime2 | Not null, createdAt + 60 days |
| assignedTo | nvarchar(100) | Not null, Foreign key references Admin.username |
| firstApprovedBy | nvarchar(100) | Foreign key references Admin.username. Null until a High risk ticket gets its first approval |
| rowVersion | rowversion | Not null, generated automatically by SQL Server and changed on every update. Used to detect two reviewers changing the same ticket at once |



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
| taxIdLastFourDigits | char(4) | Not null, used for display only |
| residentialAddress | nvarchar(200) | Not null |
| countryOfResidence | char(2) | Not null, ISO country code |
| countryOfCitizenship | char(2) | Not null, ISO country code |
| occupation | nvarchar(100) | Not null |
| employer | nvarchar(100) | Not null |

**VendorResponse**

| Attribute | Type | Constraints |
|---|---|---|
| vendorResponseId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| nameMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| dateOfBirthMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| addressMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| taxIdMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| score | int | Not null, between 0 and 50 |
| riskCodes | nvarchar(200) | Not null, ex. DeceasedSSN, CorrectionalAddress |
| VRTimeStamp | datetime2 | Not null |

**Flag**

| Attribute | Type | Constraints |
|---|---|---|
| flagId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| violationType | varchar(30) | Not null, can be one of the following: VerificationFailed, OfacMatch, ProhibitedIndustry, ClosedJurisdiction |
| raisedBy | int | Foreign key references Admin.adminId. Null when raised by the system |
| raisedAt | datetime2 | Not null |
| clearedBy | int | Foreign key references Admin.adminId. Required when the flag is cleared |
| clearedAt | datetime2 | Null until the flag is cleared |
| clearedReason | nvarchar(500) | Required when the flag is cleared |

**Decision**

| Attribute | Type | Constraints |
|---|---|---|
| decisionId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, unique (one decision per ticket, cannot be changed once saved), Foreign key references Ticket |
| decisionType | varchar(20) | Not null, can be one of the following: Approve, Reject |
| decidedBy | nvarchar(100) | Not null, Foreign key references Admin.username |
| reason | nvarchar(1000) | Not null |
| decidedAt | datetime2 | Not null |

**AuditTrail**

| Attribute | Type | Constraints |
|---|---|---|
| recId | int | Not null, Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| customerId | int | Not null, Foreign key references Customer. Should match the ticket's customerId |
| prevStatus | varchar(20) | Not null, can be one of the following: Submitted, Review, Approved, Rejected |
| currStatus | varchar(20) | Not null, can be one of the following: Submitted, Review, Approved, Rejected |
| ATMessage | nvarchar(250) | Not null |
| reason | nvarchar(500) | Not null |
| dateTimeOccurred | datetime2 | Not null |

Rows in AuditTrail will never be updated or deleted.

**Stages:**

Submitted, Review, Approved, Rejected

**Rules:**

- Every required field must be filled out and in the correct format (ex. SSN is 9 digits, date of birth is not in the future), otherwise the API returns an error and no ticket is created, so the submission is sent back before verification.
- If the vendor's verification score is below the threshold (scores run from 0 to 50 and anything under 50 fails), or the vendor returns a risk code such as a deceased SSN or a correctional facility address, a VerificationFailed flag will be raised.
- If the customer's name matches a name on the OFAC sanctions list, an OfacMatch flag will be raised, and a reviewer will compare the details to decide if it is the same person.
- Customers whose occupation, employer, or industry indicates involvement in cannabis production, distribution, or ancillary services will be flagged for review.
- Accounts based in the USA and Mexico are in the open tier and will be accepted. Everything else will be in the closed tier. This rule is applied to the customer's country of residence.
- A ticket cannot be approved until every flag on it has been cleared.
- High risk tickets will need a second approval from a different reviewer than the one who gave the first approval.
- If a ticket is not resolved within 60 days, it will be moved to Rejected. A stored procedure that runs on a daily schedule does this: it sets the status to Rejected, records a decision by the system account with the reason "Not resolved within 60 days", and writes an audit row.

The risk rating is the highest level reached by any flag on the ticket. An OfacMatch or ProhibitedIndustry flag makes it High, a ClosedJurisdiction or VerificationFailed flag makes it Medium, and a ticket with no flags is Low.

**Testing:**

xUnit tests will be implemented to ensure each rule is fulfilled.
Integration tests are to be used for status transitions.
Seed data will include tickets that each trigger one specific flag.
