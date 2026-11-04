CREATE OR ALTER PROCEDURE dbo.pr_expireReviewTickets
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @expired TABLE (TicketId int, CustomerId int);
    BEGIN TRANSACTION;
    UPDATE t WITH (UPDLOCK, ROWLOCK, READPAST)
        SET TicketStatus = 'Rejected'
        OUTPUT inserted.TicketId, inserted.CustomerId INTO @expired
        FROM dbo.Tickets t
        WHERE t.TicketStatus = 'Review' AND t.VerificationDeadline < SYSUTCDATETIME()
          AND NOT EXISTS (SELECT 1 FROM dbo.Decisions d WHERE d.TicketId = t.TicketId);
    INSERT INTO dbo.Decisions (TicketId, DecisionType, DecidedBy, Reason, DecidedAt)
        SELECT TicketId, 'Reject', N'system', N'Not resolved within 60 days', SYSUTCDATETIME() FROM @expired;
    INSERT INTO dbo.AuditTrails (TicketId, CustomerId, PrevStatus, CurrStatus, ATMessage, Reason, DateTimeOccurred)
        SELECT TicketId, CustomerId, 'Review', 'Rejected', N'Rejected automatically', N'Not resolved within 60 days', SYSUTCDATETIME() FROM @expired;
    COMMIT TRANSACTION;
END;
