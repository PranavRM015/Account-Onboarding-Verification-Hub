-- One-time copy during a coordinated write freeze. Supply SourceDatabase and ReviewDatabase with sqlcmd -v.
-- The source is read only; the target must already have Review's migrations and contain no ticket records.
USE [$(ReviewDatabase)];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_ID(N'$(SourceDatabase)') IS NULL OR DB_ID(N'$(SourceDatabase)') = DB_ID()
    THROW 51000, 'Choose an existing source database different from Review.', 1;

BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM dbo.Tickets WITH (TABLOCKX, HOLDLOCK))
   OR EXISTS (SELECT 1 FROM dbo.Flags)
   OR EXISTS (SELECT 1 FROM dbo.VendorResponses)
   OR EXISTS (SELECT 1 FROM dbo.Decisions)
   OR EXISTS (SELECT 1 FROM dbo.AuditTrails)
    THROW 51000, 'Review must be empty. This script never overwrites or merges ticket records.', 1;

SET IDENTITY_INSERT dbo.Tickets ON;
INSERT INTO dbo.Tickets (TicketId, CustomerId, JointCustomerId, TicketStatus, AccountType, RiskRating,
    CreatedAt, VerificationDeadline, AssignedTo, FirstApprovedBy, CustomerJson, JointCustomerName, RequestId, RequestHash)
SELECT t.TicketId, t.CustomerId, t.JointCustomerId, t.TicketStatus, t.AccountType, t.RiskRating,
    t.CreatedAt, t.VerificationDeadline, t.AssignedTo, t.FirstApprovedBy,
    (SELECT c.FirstName, c.MiddleName, c.LastName, c.DateOfBirth, c.Gender, c.TaxIdType,
        c.ResidentialAddress, c.CountryOfResidence, c.CountryOfCitizenship, c.Occupation, c.Employer
        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
    CASE WHEN j.CustomerId IS NOT NULL THEN j.FirstName + N' ' + j.LastName END,
    NEWID(), CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('imported-ticket:', t.TicketId)), 2)
FROM [$(SourceDatabase)].dbo.Tickets t
JOIN [$(SourceDatabase)].dbo.Customers c ON c.CustomerId = t.CustomerId
LEFT JOIN [$(SourceDatabase)].dbo.Customers j ON j.CustomerId = t.JointCustomerId;
SET IDENTITY_INSERT dbo.Tickets OFF;

IF (SELECT COUNT_BIG(*) FROM dbo.Tickets) <> (SELECT COUNT_BIG(*) FROM [$(SourceDatabase)].dbo.Tickets)
    THROW 51000, 'Some tickets lack customer profiles. Resolve the source data before copying.', 1;

SET IDENTITY_INSERT dbo.Flags ON;
INSERT INTO dbo.Flags (FlagId, TicketId, ViolationType, RaisedBy, RaisedAt, ClearedBy, ClearedByName, ClearedAt, ClearedReason)
SELECT f.FlagId, f.TicketId, f.ViolationType, f.RaisedBy, f.RaisedAt, f.ClearedBy, a.Username, f.ClearedAt, f.ClearedReason
FROM [$(SourceDatabase)].dbo.Flags f
LEFT JOIN [$(SourceDatabase)].dbo.Admins a ON a.AdminId = f.ClearedBy;
SET IDENTITY_INSERT dbo.Flags OFF;

SET IDENTITY_INSERT dbo.VendorResponses ON;
INSERT INTO dbo.VendorResponses (VendorResponseId, TicketId, NameMatch, DateOfBirthMatch, AddressMatch, TaxIdMatch, Score, RiskCodes, VRTimeStamp)
SELECT VendorResponseId, TicketId, NameMatch, DateOfBirthMatch, AddressMatch, TaxIdMatch, Score, RiskCodes, VRTimeStamp
FROM [$(SourceDatabase)].dbo.VendorResponses;
SET IDENTITY_INSERT dbo.VendorResponses OFF;

SET IDENTITY_INSERT dbo.Decisions ON;
INSERT INTO dbo.Decisions (DecisionId, TicketId, DecisionType, DecidedBy, Reason, DecidedAt)
SELECT DecisionId, TicketId, DecisionType, DecidedBy, Reason, DecidedAt FROM [$(SourceDatabase)].dbo.Decisions;
SET IDENTITY_INSERT dbo.Decisions OFF;

SET IDENTITY_INSERT dbo.AuditTrails ON;
INSERT INTO dbo.AuditTrails (RecId, TicketId, CustomerId, PrevStatus, CurrStatus, ATMessage, Reason, DateTimeOccurred)
SELECT RecId, TicketId, CustomerId, PrevStatus, CurrStatus, ATMessage, Reason, DateTimeOccurred
FROM [$(SourceDatabase)].dbo.AuditTrails;
SET IDENTITY_INSERT dbo.AuditTrails OFF;
COMMIT TRANSACTION;
