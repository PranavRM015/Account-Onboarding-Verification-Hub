-- Copies the sign-in data from the original VerificationHubDB into the identity database, so nobody has to reset a password.
-- Run it once the identity database exists (dotnet ef database update). It is safe to run again: rows already copied are skipped.
--
-- What moves:  Admins -> AdminAccounts, Customers -> CustomerAccounts (id, username, password hash, recovery question/answer, lockout state).
-- What stays:  the profile columns (names, DOB, address, tax ID, ...) belong to the intake service.
-- Not copied:  RevokedTokens. Old tokens were signed with the old HS256 key, so the identity service rejects them anyway.
--
-- The ids are kept as they are, so Tickets.CustomerId and the admin ids in the review data still line up.
-- Change the two database names below if yours differ.

SET XACT_ABORT ON;
BEGIN TRANSACTION;

INSERT INTO VerificationHub_Identity.dbo.AdminAccounts
    (AdminId, Username, PasswordHash, PasswordRecoveryQuestion, PasswordRecoveryAnswer, FailedLoginCount, LockoutEnd)
SELECT a.AdminId, a.Username, a.PasswordHash, a.PasswordRecoveryQuestion, a.PasswordRecoveryAnswer, a.FailedLoginCount, a.LockoutEnd
FROM VerificationHubDB.dbo.Admins AS a
WHERE NOT EXISTS (SELECT 1 FROM VerificationHub_Identity.dbo.AdminAccounts x WHERE x.AdminId = a.AdminId OR x.Username = a.Username);

INSERT INTO VerificationHub_Identity.dbo.CustomerAccounts
    (CustomerId, Username, PasswordHash, PasswordRecoveryQuestion, PasswordRecoveryAnswer, FailedLoginCount, LockoutEnd)
SELECT c.CustomerId, c.Username, c.PasswordHash, c.PasswordRecoveryQuestion, c.PasswordRecoveryAnswer, c.FailedLoginCount, c.LockoutEnd
FROM VerificationHubDB.dbo.Customers AS c
WHERE NOT EXISTS (SELECT 1 FROM VerificationHub_Identity.dbo.CustomerAccounts x WHERE x.CustomerId = c.CustomerId OR x.Username = c.Username);

COMMIT TRANSACTION;

-- Both pairs of numbers should match after the first run.
SELECT 'Admins'    AS [source], COUNT(*) AS [rows] FROM VerificationHubDB.dbo.Admins
UNION ALL SELECT 'AdminAccounts',    COUNT(*) FROM VerificationHub_Identity.dbo.AdminAccounts
UNION ALL SELECT 'Customers',        COUNT(*) FROM VerificationHubDB.dbo.Customers
UNION ALL SELECT 'CustomerAccounts', COUNT(*) FROM VerificationHub_Identity.dbo.CustomerAccounts;
