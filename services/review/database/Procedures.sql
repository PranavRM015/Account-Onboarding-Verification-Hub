-- Review database: auto-reject tickets still in Review after the 60-day verification deadline.
-- pr_expireReviewTickets (called hourly by TicketExpiryService) finds them and, per ticket, calls the three
-- sub-procedures inside one transaction. A failure on one ticket rolls back only that ticket.
-- Safe to re-run: every procedure is CREATE OR ALTER.

CREATE OR ALTER PROCEDURE dbo.pr_ticketExpireReject
    @ticketId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Tickets SET TicketStatus = 'Rejected'
    WHERE TicketId = @ticketId AND TicketStatus = 'Review';
END;
GO

CREATE OR ALTER PROCEDURE dbo.pr_decisionExpireReject
    @ticketId int,
    @decisionType varchar(20),
    @decidedBy nvarchar(100),
    @reason nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Decisions (TicketId, DecisionType, DecidedBy, Reason, DecidedAt)
    VALUES (@ticketId, @decisionType, @decidedBy, @reason, SYSUTCDATETIME());
END;
GO

CREATE OR ALTER PROCEDURE dbo.pr_auditTrailExpireReject
    @ticketId int,
    @customerId int,
    @prevStatus varchar(20),
    @currStatus varchar(20),
    @ATMessage nvarchar(250),
    @reason nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AuditTrails (TicketId, CustomerId, PrevStatus, CurrStatus, ATMessage, Reason, DateTimeOccurred)
    VALUES (@ticketId, @customerId, @prevStatus, @currStatus, @ATMessage, @reason, SYSUTCDATETIME());
END;
GO

CREATE OR ALTER PROCEDURE dbo.pr_expireReviewTickets
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ticketId int, @customerId int;
    DECLARE expired CURSOR LOCAL FAST_FORWARD FOR
        SELECT t.TicketId, t.CustomerId FROM dbo.Tickets t
        WHERE t.TicketStatus = 'Review' AND t.VerificationDeadline < SYSUTCDATETIME()
          AND NOT EXISTS (SELECT 1 FROM dbo.Decisions d WHERE d.TicketId = t.TicketId);
    OPEN expired;
    FETCH NEXT FROM expired INTO @ticketId, @customerId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            EXEC dbo.pr_ticketExpireReject @ticketId;
            EXEC dbo.pr_decisionExpireReject @ticketId, 'Reject', N'system', N'Not resolved within 60 days';
            EXEC dbo.pr_auditTrailExpireReject @ticketId, @customerId, 'Review', 'Rejected', N'Rejected automatically', N'Not resolved within 60 days';
            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        END CATCH;
        FETCH NEXT FROM expired INTO @ticketId, @customerId;
    END;
    CLOSE expired;
    DEALLOCATE expired;
END;
GO
