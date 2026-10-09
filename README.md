# Account Onboarding Verification Hub

**Author:** Pranav Madan

**Context:** Before a brokerage firm can open an account for a new customer, it is required to verify that the customer is who they claim to be, and that the customer does not fall outside of the firm's acceptance policies--such as being on a government sanctions list, being involved in a prohibited industry, or living in a country the firm does not accept customers from. When this review is slow or inconsistent, especially during periods where a large number of accounts are coming in at once, unverified accounts can stay open far longer than they should. An excellent way to deal with this is to keep all of the customer's information in one central place, and move each new account through a series of stages as a ticket, so that every account is checked against the same set of rules before it is approved. Additionally, every action taken on a ticket will be stored in a permanent record, so that the firm can show who reviewed each account, what they decided, and why.

**Goal (one sentence):** This application will verify the identity of incoming brokerage customers, flag those who do not meet the firm's acceptance rules for review, and keep a permanent record of every decision that is made.

**Overview:** The Account Onboarding Verification Hub will accept tickets from an Angular based form containing the customer's information--including the customer's name, date of birth, residential address, tax ID, occupation, employer, and country of residence. The API will validate the submission and save it to the database as a new ticket. The backend will consist of a validation pipeline containing stages dealing with format checking, identity verification (through a mocked third-party vendor), and screening the customer against the firm's rules. Any problems found along the way will be raised as flags. The system will not reject a customer on its own; instead, a reviewer will look at each flag and decide whether to clear it, restrict the account, or deny it. High risk tickets will need a second approval from a different reviewer before they can be approved.

**Tech Stack:**

| Aspect | Tool |
|---|---|
| Frontend | Angular, TypeScript, HTML, CSS |
| Backend | ASP.NET Core (C#) |
| Object Relational Mapper | Entity Framework Core |
| Database | Microsoft SQL Server |
| Authentication | ASP.NET Core Identity |
| Hosting | Local (MVP), Azure (post-MVP) |
| Testing | xUnit (.NET), Jasmine/Karma (Angular) |

**Schema Design:**

**Tables**

**Ticket**

| Attribute | Type | Constraints |
|---|---|---|
| ticketId | int | Not null, Identity(1,1), Primary key |
| customerId | int | Not null, Foreign key references Customer |
| status | varchar(20) | Not null, default Submitted. Can be one of the following: Submitted, FormatVerification, Review, Approved, Rejected |
| accountType | varchar(15) | Not null, can be one of the following: Individual, Joint |
| riskRating | varchar(6) | Can be one of the following: Low, Medium, High. Null until scored |
| createdAt | datetime2 | Not null |
| verificationDeadline | datetime2 | Not null, createdAt + 60 days |
| assignedTo | nvarchar(450) |  |

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
| taxIdHash | binary(32) | Not null, used to look up duplicates without storing the full number |
| taxIdLastFourDigits | char(4) | Not null, used for display only |
| residentialAddress | nvarchar(200) | Not null |
| countryOfResidence | char(2) | Not null, ISO country code |
| countryOfCitizenship | char(2) | Not null, ISO country code |
| occupation | nvarchar(100) | Not null |
| employer | nvarchar(100) | Not null |

**VendorResponse**

| Attribute | Type | Constraints |
|---|---|---|
| vendorResponseId | int | Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| nameMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| dateOfBirthMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| addressMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| taxIdMatch | varchar(7) | Not null, Can be one of the following: Match, Partial, NoMatch |
| score | int | Not null, between 0 and 50 |
| riskCodes | nvarchar(200) | Not null, ex. DeceasedSSN, CorrectionalAddress |
| timeStamp | datetime2 | Not null |

**Flag**

| Attribute | Type | Constraints |
|---|---|---|
| flagId | int | Identity(1,1), Primary key, Not null |
| ticketId | int | Not null, Foreign key references Ticket |
| violationType | varchar(30) | Not null, can be one of the following: VerificationFailed, OfacMatch, ProhibitedIndustry, ClosedJurisdiction |
| raisedBy | nvarchar(450) | Foreign key references AspNetUsers. Null when raised by the system |
| raisedAt | datetime2 | Not null |
| clearedBy | nvarchar(450) |  |
| clearedAt | datetime2 | Not null |
| clearedReason | nvarchar(500) | Required when the flag is cleared |

**Decision**

| Attribute | Type | Constraints |
|---|---|---|
| decisionId | int | Identity(1,1), Primary key, Not null |
| ticketId | int | Not null, Foreign key references Ticket |
| decisionType | varchar(20) | Not null, can be one of the following: Approve, SecondApproval, Restrict, Deny |
| decidedBy | nvarchar(450) | Not null |
| reason | nvarchar(1000) | Not null |
| decidedAt | datetime2 | Not null |

**AuditTrail**

| Attribute | Type | Constraints |
|---|---|---|
| recId | int | Identity(1,1), Primary key |
| ticketId | int | Not null, Foreign key references Ticket |
| userId | nvarchar(450) | Not null |
| prevStatus | varchar(20) | Not null |
| currStatus | varchar(20) | Not null |
| message | nvarchar(250) | Not null |
| reason | nvarchar(500) | Not null |
| dateTimeOccurred | datetime2 | Not null |

Rows in AuditTrail will never be updated or deleted.

**Stages:**

Submitted, FormatVerification, Review, Approved, Rejected

**Rules:**

- Every required field must be filled out and in the correct format (ex. SSN is 9 digits, date of birth is not in the future), otherwise the ticket will be sent back before verification.
- If the vendor's verification score is below the threshold, or the vendor returns a risk code such as a deceased SSN or a correctional facility address, a VerificationFailed flag will be raised.
- If the customer's name matches a name on the OFAC sanctions list, an OfacMatch flag will be raised, and a reviewer will compare the details to decide if it is the same person.
- Customers whose occupation, employer, or industry indicates involvement in cannabis production, distribution, or ancillary services will be flagged for review.
- Accounts based in Mexico are in the open tier and will be accepted. Maintain tier countries (Canada is used as an example) will only be accepted for existing customers, while everything else will be in the closed tier. This rule is applied to the customer's country of residence.
- A ticket cannot be approved until every flag on it has been cleared.
- High risk tickets will need a second approval from a different reviewer than the one who gave the first approval.
- If a ticket is not resolved within 60 days, it will be moved to Rejected.

How the risk rating is calculated has not been determined yet.

**Testing:**

xUnit tests will be implemented to ensure each rule is fulfilled.
Integration tests are to be used for status transitions.
Seed data will include tickets that each trigger one specific flag.
