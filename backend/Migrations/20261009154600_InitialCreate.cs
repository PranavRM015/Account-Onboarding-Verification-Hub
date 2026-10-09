using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    customerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    firstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    middleName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    lastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    dateOfBirth = table.Column<DateTime>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: false),
                    taxIdType = table.Column<string>(type: "varchar(4)", maxLength: 4, nullable: false),
                    taxIdHash = table.Column<byte[]>(type: "binary(32)", maxLength: 32, nullable: false),
                    taxIdLastFourDigits = table.Column<string>(type: "char(4)", maxLength: 4, nullable: false),
                    residentialAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    countryOfResidence = table.Column<string>(type: "char(2)", maxLength: 2, nullable: false),
                    countryOfCitizenship = table.Column<string>(type: "char(2)", maxLength: 2, nullable: false),
                    occupation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    employer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.customerId);
                    table.CheckConstraint("CK_Gender", "gender IN ('Male', 'Female', 'Other')");
                    table.CheckConstraint("CK_ResidentialAddress", "LEN(residentialAddress) > 0");
                    table.CheckConstraint("CK_TaxIdLastFourDigits", "LEN(taxIdLastFourDigits) = 4");
                    table.CheckConstraint("CK_TaxIdType", "taxIdType IN ('SSN', 'ITIN', 'EIN')");
                });

            migrationBuilder.CreateTable(
                name: "Tickets",
                columns: table => new
                {
                    ticketId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customerId = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Submitted"),
                    accountType = table.Column<string>(type: "varchar(15)", maxLength: 15, nullable: false),
                    riskRating = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    verificationDeadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    assignedTo = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.ticketId);
                    table.CheckConstraint("CK_AccountType", "accountType IN ('Individual', 'Joint')");
                    table.CheckConstraint("CK_RiskRating", "riskRating IS NULL OR riskRating IN ('Low', 'Medium', 'High')");
                    table.CheckConstraint("CK_Status", "status IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_VerificationDeadline", "verificationDeadline = DATEADD(DAY, 60, createdAt)");
                    table.ForeignKey(
                        name: "FK_Tickets_Customers_customerId",
                        column: x => x.customerId,
                        principalTable: "Customers",
                        principalColumn: "customerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditTrails",
                columns: table => new
                {
                    recId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticketId = table.Column<int>(type: "int", nullable: false),
                    userId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    prevStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    currStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    dateTimeOccurred = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrails", x => x.recId);
                    table.CheckConstraint("CK_AuditCurrStatus", "currStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
                    table.CheckConstraint("CK_AuditPrevStatus", "prevStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_AuditTrails_Tickets_ticketId",
                        column: x => x.ticketId,
                        principalTable: "Tickets",
                        principalColumn: "ticketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Decisions",
                columns: table => new
                {
                    decisionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticketId = table.Column<int>(type: "int", nullable: false),
                    decisionType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    decidedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    decidedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Decisions", x => x.decisionId);
                    table.CheckConstraint("CK_DecisionReason", "reason IS NOT NULL AND LEN(reason) > 0");
                    table.CheckConstraint("CK_DecisionType", "decisionType IN ('Approve', 'Deny')");
                    table.ForeignKey(
                        name: "FK_Decisions_Tickets_ticketId",
                        column: x => x.ticketId,
                        principalTable: "Tickets",
                        principalColumn: "ticketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Flags",
                columns: table => new
                {
                    flagId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticketId = table.Column<int>(type: "int", nullable: false),
                    violationType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    raisedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    raisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    clearedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    clearedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    clearedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Flags", x => x.flagId);
                    table.CheckConstraint("CK_ClearedReason", "clearedAt IS NULL OR (clearedBy IS NOT NULL AND clearedReason IS NOT NULL AND LEN(clearedReason) > 0)");
                    table.CheckConstraint("CK_ViolationType", "violationType IN ('VerificationFailed', 'OfacMatch', 'ProhibitedIndustry', 'ClosedJurisdiction')");
                    table.ForeignKey(
                        name: "FK_Flags_Tickets_ticketId",
                        column: x => x.ticketId,
                        principalTable: "Tickets",
                        principalColumn: "ticketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorResponses",
                columns: table => new
                {
                    vendorResponseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ticketId = table.Column<int>(type: "int", nullable: false),
                    nameMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    dateOfBirthMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    addressMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    taxIdMatch = table.Column<string>(type: "varchar(7)", maxLength: 7, nullable: false),
                    score = table.Column<int>(type: "int", nullable: false),
                    riskCodes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    timeStamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorResponses", x => x.vendorResponseId);
                    table.CheckConstraint("CK_AddressMatch", "addressMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.CheckConstraint("CK_DateOfBirthMatch", "dateOfBirthMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.CheckConstraint("CK_NameMatch", "nameMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.CheckConstraint("CK_Score", "score BETWEEN 0 AND 50");
                    table.CheckConstraint("CK_TaxIdMatch", "taxIdMatch IN ('Match', 'Partial', 'NoMatch')");
                    table.ForeignKey(
                        name: "FK_VendorResponses_Tickets_ticketId",
                        column: x => x.ticketId,
                        principalTable: "Tickets",
                        principalColumn: "ticketId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_ticketId",
                table: "AuditTrails",
                column: "ticketId");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_ticketId",
                table: "Decisions",
                column: "ticketId");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_ticketId",
                table: "Flags",
                column: "ticketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_customerId",
                table: "Tickets",
                column: "customerId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorResponses_ticketId",
                table: "VendorResponses",
                column: "ticketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
