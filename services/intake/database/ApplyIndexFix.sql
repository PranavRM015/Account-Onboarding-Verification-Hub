-- ApplyIndexFix.sql
-- Recreates UX_Customers_Username as a filtered unique index so that
-- Intake can insert customer profiles with Username = '' (the empty-string
-- placeholder it stores until Identity writes the real value).
--
-- When to run:
--   Run this once against VerificationHubDB (the shared monolith database)
--   before running Intake in microservices mode. It is not needed when
--   Intake has its own database (VerificationHub_Intake), because IntakeDbContext
--   will create the index with the correct filter from the start.
--
-- How to run:
--   sqlcmd -S .\SQLEXPRESS -d VerificationHubDB -E -C -b -i services/intake/database/ApplyIndexFix.sql
--   Or open it in SSMS, set the target database to VerificationHubDB, and execute.
--
-- Safe to run more than once. If the filtered index already exists the script
-- exits cleanly without touching the database.
-- ============================================================================

SET NOCOUNT ON;

-- ── Step 1: Check whether the index already has the correct filter ───────────

DECLARE @currentFilter nvarchar(500);

SELECT @currentFilter = filter_definition
FROM   sys.indexes
WHERE  name      = 'UX_Customers_Username'
  AND  object_id = OBJECT_ID('dbo.Customers');

IF @currentFilter IS NOT NULL AND @currentFilter = '([Username]<>'''')'
BEGIN
    PRINT 'UX_Customers_Username already has the correct filter. No changes made.';
    -- Print the current definition as confirmation.
    SELECT name, is_unique, filter_definition AS current_filter
    FROM   sys.indexes
    WHERE  name      = 'UX_Customers_Username'
      AND  object_id = OBJECT_ID('dbo.Customers');
    RETURN;
END;

-- If the index exists but with an unexpected definition, stop and report.
IF @currentFilter IS NOT NULL AND @currentFilter <> '([Username]<>'''')'
BEGIN
    PRINT 'ERROR: UX_Customers_Username exists but has an unexpected filter definition.';
    PRINT 'Found: ' + @currentFilter;
    PRINT 'Review the index definition manually before running this script.';
    -- Raise an error so sqlcmd -b and SSMS both see a failure.
    RAISERROR('Unexpected index definition on UX_Customers_Username. No changes made.', 16, 1);
    RETURN;
END;

-- ── Step 2: Guard against duplicate non-empty usernames ──────────────────────
-- The new filtered index enforces uniqueness only for non-empty values.
-- If duplicates already exist for real usernames the CREATE INDEX would fail;
-- this check surfaces the problem clearly before we start.

IF EXISTS (
    SELECT Username
    FROM   dbo.Customers
    WHERE  Username <> N''
    GROUP  BY Username
    HAVING COUNT(*) > 1
)
BEGIN
    PRINT 'ERROR: Duplicate non-empty Username values exist in dbo.Customers.';
    PRINT 'Resolve duplicates before running this script.';
    RAISERROR('Duplicate non-empty usernames found. No changes made.', 16, 1);
    RETURN;
END;

-- ── Step 3: Recreate the index inside a transaction ──────────────────────────
-- DROP + CREATE are DDL and cannot be inside a user transaction on SQL Server,
-- but we wrap the work so that any failure during the CREATE raises a clear error.
-- If CREATE fails the index is gone; run the script again once the cause is fixed.

PRINT 'Dropping UX_Customers_Username ...';

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  name      = 'UX_Customers_Username'
      AND  object_id = OBJECT_ID('dbo.Customers')
)
    DROP INDEX [UX_Customers_Username] ON [dbo].[Customers];

PRINT 'Creating filtered UX_Customers_Username ...';

CREATE UNIQUE INDEX [UX_Customers_Username]
    ON [dbo].[Customers] ([Username])
    WHERE [Username] <> N'';

PRINT 'Index created successfully.';

-- ── Step 4: Verification query ───────────────────────────────────────────────
-- Should return: is_unique = 1, filter_definition = ([Username]<>'')

SELECT name            AS index_name,
       is_unique,
       filter_definition
FROM   sys.indexes
WHERE  name      = 'UX_Customers_Username'
  AND  object_id = OBJECT_ID('dbo.Customers');
