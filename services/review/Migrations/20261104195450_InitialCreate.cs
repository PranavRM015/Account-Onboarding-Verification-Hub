using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Review.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    TicketId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    JointCustomerId = table.Column<int>(type: "int", nullable: true),
                    TicketStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Submitted"),
                    AccountType = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: false),
                    RiskRating = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerificationDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FirstApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JointCustomerName = table.Column<string>(type: "nvarchar(151)", maxLength: 151, nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.TicketId);
                    table.CheckConstraint("CK_AccountType", "AccountType IN ('Individual', 'Joint')");
                    table.CheckConstraint("CK_JointAccount", "(AccountType = 'Joint' AND JointCustomerId IS NOT NULL AND JointCustomerId <> CustomerId) OR (AccountType = 'Individual' AND JointCustomerId IS NULL)");
                    table.CheckConstraint("CK_RiskRating", "RiskRating IS NULL OR RiskRating IN ('Low', 'Medium', 'High')");
                    table.CheckConstraint("CK_Status", "TicketStatus IN ('Submitted', 'Review', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_VerificationDeadline", "VerificationDeadline = DATEADD(DAY, 60, CreatedAt)");
                });

            migrationBuilder.CreateTable(
                name: "AuditTrails",
                columns: table => new
                {
                    RecId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    PrevStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    CurrStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ATMessage = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DateTimeOccurred = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrails", x => x.RecId);
                    table.CheckConstraint("CK_AuditCurrStatus", "CurrStatus IN ('Submitted', 'Review', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_AuditPrevStatus", "PrevStatus IN ('Submitted', 'Review', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_AuditTrails_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Decisions",
                columns: table => new
                {
                    DecisionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    DecisionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decisions", x => x.DecisionId);
                    table.CheckConstraint("CK_DecisionReason", "Reason IS NOT NULL AND LEN(Reason) > 0");
                    table.CheckConstraint("CK_DecisionType", "DecisionType IN ('Approve', 'Reject')");
                    table.ForeignKey(
                        name: "FK_Decisions_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Flags",
                columns: table => new
                {
                    FlagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    ViolationType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    RaisedBy = table.Column<int>(type: "int", nullable: true),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClearedBy = table.Column<int>(type: "int", nullable: true),
                    ClearedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClearedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flags", x => x.FlagId);
                    table.CheckConstraint("CK_ViolationType", "ViolationType IN ('VerificationFailed', 'OfacMatch', 'ProhibitedIndustry', 'ClosedJurisdiction')");
                    table.ForeignKey(
                        name: "FK_Flags_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorResponses",
                columns: table => new
                {
                    VendorResponseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    NameMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    DateOfBirthMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    AddressMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    TaxIdMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    RiskCodes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VRTimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorResponses", x => x.VendorResponseId);
                    table.CheckConstraint("CK_AddressMatch", "AddressMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.CheckConstraint("CK_DateOfBirthMatch", "DateOfBirthMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.CheckConstraint("CK_NameMatch", "NameMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.CheckConstraint("CK_Score", "Score BETWEEN 0 AND 50");
                    table.CheckConstraint("CK_TaxIdMatch", "TaxIdMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.ForeignKey(
                        name: "FK_VendorResponses_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "TicketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_TicketId",
                table: "AuditTrails",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_TicketId",
                table: "Decisions",
                column: "TicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Flags_TicketId",
                table: "Flags",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CustomerId",
                table: "Tickets",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_RequestId",
                table: "Tickets",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorResponses_TicketId",
                table: "VendorResponses",
                column: "TicketId");
            migrationBuilder.Sql("""
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

""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.pr_expireReviewTickets");
            migrationBuilder.DropTable(
                name: "AuditTrails");

            migrationBuilder.DropTable(
                name: "Decisions");

            migrationBuilder.DropTable(
                name: "Flags");

            migrationBuilder.DropTable(
                name: "VendorResponses");

            migrationBuilder.DropTable(
                name: "Tickets");
        }
    }
}
