use VerificationHubDB;

INSERT INTO Admins (Username, PasswordHash) VALUES (N'system', N'disabled');
GO

CREATE PROCEDURE pr_ticketExpireReject
    @ticketId int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Tickets SET TicketStatus = 'Rejected'
    WHERE TicketId = @ticketId AND TicketStatus = 'Review';
END;
GO

CREATE PROCEDURE pr_decisionExpireReject
    @ticketId int,
    @decisionType varchar(20),
    @decidedBy nvarchar(100),
    @reason nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Decisions (TicketId, DecisionType, DecidedBy, Reason, DecidedAt)
    VALUES (@ticketId, @decisionType, @decidedBy, @reason, SYSUTCDATETIME());
END;
GO

CREATE PROCEDURE pr_auditTrailExpireReject
    @ticketId int,
    @customerId int,
    @prevStatus varchar(20),
    @currStatus varchar(20),
    @ATMessage nvarchar(250),
    @reason nvarchar(500)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO AuditTrails (TicketId, CustomerId, PrevStatus, CurrStatus, ATMessage, Reason, DateTimeOccurred)
    VALUES (@ticketId, @customerId, @prevStatus, @currStatus, @ATMessage, @reason, SYSUTCDATETIME());
END;
GO

CREATE PROCEDURE pr_expireReviewTickets
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ticketId int, @customerId int;
    DECLARE expired CURSOR LOCAL FAST_FORWARD FOR
        SELECT t.TicketId, t.CustomerId FROM Tickets t
        WHERE t.TicketStatus = 'Review' AND t.VerificationDeadline < SYSUTCDATETIME()
          AND NOT EXISTS (SELECT 1 FROM Decisions d WHERE d.TicketId = t.TicketId);
    OPEN expired;
    FETCH NEXT FROM expired INTO @ticketId, @customerId;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;
            EXEC pr_ticketExpireReject @ticketId;
            EXEC pr_decisionExpireReject @ticketId, 'Reject', N'system', N'Not resolved within 60 days';
            EXEC pr_auditTrailExpireReject @ticketId, @customerId, 'Review', 'Rejected', N'Rejected automatically', N'Not resolved within 60 days';
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

select 1;