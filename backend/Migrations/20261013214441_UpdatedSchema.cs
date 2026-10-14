using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrails_Customers_customerId",
                table: "AuditTrails");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrails_Tickets_ticketId",
                table: "AuditTrails");

            migrationBuilder.DropForeignKey(
                name: "FK_Decisions_Admins_decidedBy",
                table: "Decisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Decisions_Tickets_ticketId",
                table: "Decisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Admins_clearedBy",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Admins_raisedBy",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Tickets_ticketId",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Admins_assignedTo",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Customers_customerId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorResponses_Tickets_ticketId",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AddressMatch",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DateOfBirthMatch",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NameMatch",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Score",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxIdMatch",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountType",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RiskRating",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Status",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VerificationDeadline",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ViolationType",
                table: "Flags");

            migrationBuilder.DropIndex(
                name: "IX_Decisions_ticketId",
                table: "Decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DecisionReason",
                table: "Decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Gender",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ResidentialAddress",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxIdLastFourDigits",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxIdType",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Admins_username",
                table: "Admins");

            migrationBuilder.RenameColumn(
                name: "timeStamp",
                table: "VendorResponses",
                newName: "TimeStamp");

            migrationBuilder.RenameColumn(
                name: "ticketId",
                table: "VendorResponses",
                newName: "TicketId");

            migrationBuilder.RenameColumn(
                name: "taxIdMatch",
                table: "VendorResponses",
                newName: "TaxIdMatch");

            migrationBuilder.RenameColumn(
                name: "score",
                table: "VendorResponses",
                newName: "Score");

            migrationBuilder.RenameColumn(
                name: "riskCodes",
                table: "VendorResponses",
                newName: "RiskCodes");

            migrationBuilder.RenameColumn(
                name: "nameMatch",
                table: "VendorResponses",
                newName: "NameMatch");

            migrationBuilder.RenameColumn(
                name: "dateOfBirthMatch",
                table: "VendorResponses",
                newName: "DateOfBirthMatch");

            migrationBuilder.RenameColumn(
                name: "addressMatch",
                table: "VendorResponses",
                newName: "AddressMatch");

            migrationBuilder.RenameColumn(
                name: "vendorResponseId",
                table: "VendorResponses",
                newName: "VendorResponseId");

            migrationBuilder.RenameIndex(
                name: "IX_VendorResponses_ticketId",
                table: "VendorResponses",
                newName: "IX_VendorResponses_TicketId");

            migrationBuilder.RenameColumn(
                name: "verificationDeadline",
                table: "Tickets",
                newName: "VerificationDeadline");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "Tickets",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "riskRating",
                table: "Tickets",
                newName: "RiskRating");

            migrationBuilder.RenameColumn(
                name: "customerId",
                table: "Tickets",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "createdAt",
                table: "Tickets",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "assignedTo",
                table: "Tickets",
                newName: "AssignedTo");

            migrationBuilder.RenameColumn(
                name: "accountType",
                table: "Tickets",
                newName: "AccountType");

            migrationBuilder.RenameColumn(
                name: "ticketId",
                table: "Tickets",
                newName: "TicketId");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_customerId",
                table: "Tickets",
                newName: "IX_Tickets_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_assignedTo",
                table: "Tickets",
                newName: "IX_Tickets_AssignedTo");

            migrationBuilder.RenameColumn(
                name: "violationType",
                table: "Flags",
                newName: "ViolationType");

            migrationBuilder.RenameColumn(
                name: "ticketId",
                table: "Flags",
                newName: "TicketId");

            migrationBuilder.RenameColumn(
                name: "raisedBy",
                table: "Flags",
                newName: "RaisedBy");

            migrationBuilder.RenameColumn(
                name: "raisedAt",
                table: "Flags",
                newName: "RaisedAt");

            migrationBuilder.RenameColumn(
                name: "clearedReason",
                table: "Flags",
                newName: "ClearedReason");

            migrationBuilder.RenameColumn(
                name: "clearedBy",
                table: "Flags",
                newName: "ClearedBy");

            migrationBuilder.RenameColumn(
                name: "clearedAt",
                table: "Flags",
                newName: "ClearedAt");

            migrationBuilder.RenameColumn(
                name: "flagId",
                table: "Flags",
                newName: "FlagId");

            migrationBuilder.RenameIndex(
                name: "IX_Flags_ticketId",
                table: "Flags",
                newName: "IX_Flags_TicketId");

            migrationBuilder.RenameIndex(
                name: "IX_Flags_raisedBy",
                table: "Flags",
                newName: "IX_Flags_RaisedBy");

            migrationBuilder.RenameIndex(
                name: "IX_Flags_clearedBy",
                table: "Flags",
                newName: "IX_Flags_ClearedBy");

            migrationBuilder.RenameColumn(
                name: "ticketId",
                table: "Decisions",
                newName: "TicketId");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "Decisions",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "decisionType",
                table: "Decisions",
                newName: "DecisionType");

            migrationBuilder.RenameColumn(
                name: "decidedBy",
                table: "Decisions",
                newName: "DecidedBy");

            migrationBuilder.RenameColumn(
                name: "decidedAt",
                table: "Decisions",
                newName: "DecidedAt");

            migrationBuilder.RenameColumn(
                name: "decisionId",
                table: "Decisions",
                newName: "DecisionId");

            migrationBuilder.RenameIndex(
                name: "IX_Decisions_decidedBy",
                table: "Decisions",
                newName: "IX_Decisions_DecidedBy");

            migrationBuilder.RenameColumn(
                name: "taxIdType",
                table: "Customers",
                newName: "TaxIdType");

            migrationBuilder.RenameColumn(
                name: "taxIdLastFourDigits",
                table: "Customers",
                newName: "TaxIdLastFourDigits");

            migrationBuilder.RenameColumn(
                name: "taxIdHash",
                table: "Customers",
                newName: "TaxIdHash");

            migrationBuilder.RenameColumn(
                name: "residentialAddress",
                table: "Customers",
                newName: "ResidentialAddress");

            migrationBuilder.RenameColumn(
                name: "occupation",
                table: "Customers",
                newName: "Occupation");

            migrationBuilder.RenameColumn(
                name: "middleName",
                table: "Customers",
                newName: "MiddleName");

            migrationBuilder.RenameColumn(
                name: "lastName",
                table: "Customers",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "gender",
                table: "Customers",
                newName: "Gender");

            migrationBuilder.RenameColumn(
                name: "firstName",
                table: "Customers",
                newName: "FirstName");

            migrationBuilder.RenameColumn(
                name: "employer",
                table: "Customers",
                newName: "Employer");

            migrationBuilder.RenameColumn(
                name: "dateOfBirth",
                table: "Customers",
                newName: "DateOfBirth");

            migrationBuilder.RenameColumn(
                name: "countryOfResidence",
                table: "Customers",
                newName: "CountryOfResidence");

            migrationBuilder.RenameColumn(
                name: "countryOfCitizenship",
                table: "Customers",
                newName: "CountryOfCitizenship");

            migrationBuilder.RenameColumn(
                name: "customerId",
                table: "Customers",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "ticketId",
                table: "AuditTrails",
                newName: "TicketId");

            migrationBuilder.RenameColumn(
                name: "reason",
                table: "AuditTrails",
                newName: "Reason");

            migrationBuilder.RenameColumn(
                name: "prevStatus",
                table: "AuditTrails",
                newName: "PrevStatus");

            migrationBuilder.RenameColumn(
                name: "message",
                table: "AuditTrails",
                newName: "Message");

            migrationBuilder.RenameColumn(
                name: "dateTimeOccurred",
                table: "AuditTrails",
                newName: "DateTimeOccurred");

            migrationBuilder.RenameColumn(
                name: "customerId",
                table: "AuditTrails",
                newName: "CustomerId");

            migrationBuilder.RenameColumn(
                name: "currStatus",
                table: "AuditTrails",
                newName: "CurrStatus");

            migrationBuilder.RenameColumn(
                name: "recId",
                table: "AuditTrails",
                newName: "RecId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditTrails_ticketId",
                table: "AuditTrails",
                newName: "IX_AuditTrails_TicketId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditTrails_customerId",
                table: "AuditTrails",
                newName: "IX_AuditTrails_CustomerId");

            migrationBuilder.RenameColumn(
                name: "username",
                table: "Admins",
                newName: "Username");

            migrationBuilder.RenameColumn(
                name: "passwordhash",
                table: "Admins",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "AdminID",
                table: "Admins",
                newName: "AdminId");

            migrationBuilder.AddColumn<int>(
                name: "JointCustomerId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Admins",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Admins_Username",
                table: "Admins",
                column: "Username");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AddressMatch",
                table: "VendorResponses",
                sql: "AddressMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DateOfBirthMatch",
                table: "VendorResponses",
                sql: "DateOfBirthMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NameMatch",
                table: "VendorResponses",
                sql: "NameMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Score",
                table: "VendorResponses",
                sql: "Score BETWEEN 0 AND 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxIdMatch",
                table: "VendorResponses",
                sql: "TaxIdMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_JointCustomerId",
                table: "Tickets",
                column: "JointCustomerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountType",
                table: "Tickets",
                sql: "AccountType IN ('Individual', 'Joint')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_JointAccount",
                table: "Tickets",
                sql: "(AccountType = 'Joint' AND JointCustomerId IS NOT NULL AND JointCustomerId <> CustomerId) OR (AccountType = 'Individual' AND JointCustomerId IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RiskRating",
                table: "Tickets",
                sql: "RiskRating IS NULL OR RiskRating IN ('Low', 'Medium', 'High')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Status",
                table: "Tickets",
                sql: "Status IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VerificationDeadline",
                table: "Tickets",
                sql: "VerificationDeadline = DATEADD(DAY, 60, CreatedAt)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ViolationType",
                table: "Flags",
                sql: "ViolationType IN ('VerificationFailed', 'OfacMatch', 'ProhibitedIndustry', 'ClosedJurisdiction')");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_TicketId",
                table: "Decisions",
                column: "TicketId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DecisionReason",
                table: "Decisions",
                sql: "Reason IS NOT NULL AND LEN(Reason) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions",
                sql: "DecisionType IN ('Approve', 'Deny')");

            migrationBuilder.CreateIndex(
                name: "UX_Customers_TaxIdHash",
                table: "Customers",
                column: "TaxIdHash",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gender",
                table: "Customers",
                sql: "Gender IN ('Male', 'Female', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ResidentialAddress",
                table: "Customers",
                sql: "LEN(ResidentialAddress) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxIdLastFourDigits",
                table: "Customers",
                sql: "LEN(TaxIdLastFourDigits) = 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxIdType",
                table: "Customers",
                sql: "TaxIdType IN ('SSN', 'ITIN', 'EIN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails",
                sql: "CurrStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails",
                sql: "PrevStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrails_Customers_CustomerId",
                table: "AuditTrails",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrails_Tickets_TicketId",
                table: "AuditTrails",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "TicketId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Decisions_Admins_DecidedBy",
                table: "Decisions",
                column: "DecidedBy",
                principalTable: "Admins",
                principalColumn: "Username",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Decisions_Tickets_TicketId",
                table: "Decisions",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "TicketId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flags_Admins_ClearedBy",
                table: "Flags",
                column: "ClearedBy",
                principalTable: "Admins",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flags_Admins_RaisedBy",
                table: "Flags",
                column: "RaisedBy",
                principalTable: "Admins",
                principalColumn: "AdminId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flags_Tickets_TicketId",
                table: "Flags",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "TicketId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Admins_AssignedTo",
                table: "Tickets",
                column: "AssignedTo",
                principalTable: "Admins",
                principalColumn: "Username",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Customers_CustomerId",
                table: "Tickets",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Customers_JointCustomerId",
                table: "Tickets",
                column: "JointCustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorResponses_Tickets_TicketId",
                table: "VendorResponses",
                column: "TicketId",
                principalTable: "Tickets",
                principalColumn: "TicketId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrails_Customers_CustomerId",
                table: "AuditTrails");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrails_Tickets_TicketId",
                table: "AuditTrails");

            migrationBuilder.DropForeignKey(
                name: "FK_Decisions_Admins_DecidedBy",
                table: "Decisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Decisions_Tickets_TicketId",
                table: "Decisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Admins_ClearedBy",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Admins_RaisedBy",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Tickets_TicketId",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Admins_AssignedTo",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Customers_CustomerId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Customers_JointCustomerId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorResponses_Tickets_TicketId",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AddressMatch",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DateOfBirthMatch",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NameMatch",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Score",
                table: "VendorResponses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxIdMatch",
                table: "VendorResponses");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_JointCustomerId",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountType",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_JointAccount",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RiskRating",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Status",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VerificationDeadline",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ViolationType",
                table: "Flags");

            migrationBuilder.DropIndex(
                name: "IX_Decisions_TicketId",
                table: "Decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DecisionReason",
                table: "Decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions");

            migrationBuilder.DropIndex(
                name: "UX_Customers_TaxIdHash",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Gender",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ResidentialAddress",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxIdLastFourDigits",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaxIdType",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Admins_Username",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "JointCustomerId",
                table: "Tickets");

            migrationBuilder.RenameColumn(
                name: "TimeStamp",
                table: "VendorResponses",
                newName: "timeStamp");

            migrationBuilder.RenameColumn(
                name: "TicketId",
                table: "VendorResponses",
                newName: "ticketId");

            migrationBuilder.RenameColumn(
                name: "TaxIdMatch",
                table: "VendorResponses",
                newName: "taxIdMatch");

            migrationBuilder.RenameColumn(
                name: "Score",
                table: "VendorResponses",
                newName: "score");

            migrationBuilder.RenameColumn(
                name: "RiskCodes",
                table: "VendorResponses",
                newName: "riskCodes");

            migrationBuilder.RenameColumn(
                name: "NameMatch",
                table: "VendorResponses",
                newName: "nameMatch");

            migrationBuilder.RenameColumn(
                name: "DateOfBirthMatch",
                table: "VendorResponses",
                newName: "dateOfBirthMatch");

            migrationBuilder.RenameColumn(
                name: "AddressMatch",
                table: "VendorResponses",
                newName: "addressMatch");

            migrationBuilder.RenameColumn(
                name: "VendorResponseId",
                table: "VendorResponses",
                newName: "vendorResponseId");

            migrationBuilder.RenameIndex(
                name: "IX_VendorResponses_TicketId",
                table: "VendorResponses",
                newName: "IX_VendorResponses_ticketId");

            migrationBuilder.RenameColumn(
                name: "VerificationDeadline",
                table: "Tickets",
                newName: "verificationDeadline");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Tickets",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "RiskRating",
                table: "Tickets",
                newName: "riskRating");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "Tickets",
                newName: "customerId");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Tickets",
                newName: "createdAt");

            migrationBuilder.RenameColumn(
                name: "AssignedTo",
                table: "Tickets",
                newName: "assignedTo");

            migrationBuilder.RenameColumn(
                name: "AccountType",
                table: "Tickets",
                newName: "accountType");

            migrationBuilder.RenameColumn(
                name: "TicketId",
                table: "Tickets",
                newName: "ticketId");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_CustomerId",
                table: "Tickets",
                newName: "IX_Tickets_customerId");

            migrationBuilder.RenameIndex(
                name: "IX_Tickets_AssignedTo",
                table: "Tickets",
                newName: "IX_Tickets_assignedTo");

            migrationBuilder.RenameColumn(
                name: "ViolationType",
                table: "Flags",
                newName: "violationType");

            migrationBuilder.RenameColumn(
                name: "TicketId",
                table: "Flags",
                newName: "ticketId");

            migrationBuilder.RenameColumn(
                name: "RaisedBy",
                table: "Flags",
                newName: "raisedBy");

            migrationBuilder.RenameColumn(
                name: "RaisedAt",
                table: "Flags",
                newName: "raisedAt");

            migrationBuilder.RenameColumn(
                name: "ClearedReason",
                table: "Flags",
                newName: "clearedReason");

            migrationBuilder.RenameColumn(
                name: "ClearedBy",
                table: "Flags",
                newName: "clearedBy");

            migrationBuilder.RenameColumn(
                name: "ClearedAt",
                table: "Flags",
                newName: "clearedAt");

            migrationBuilder.RenameColumn(
                name: "FlagId",
                table: "Flags",
                newName: "flagId");

            migrationBuilder.RenameIndex(
                name: "IX_Flags_TicketId",
                table: "Flags",
                newName: "IX_Flags_ticketId");

            migrationBuilder.RenameIndex(
                name: "IX_Flags_RaisedBy",
                table: "Flags",
                newName: "IX_Flags_raisedBy");

            migrationBuilder.RenameIndex(
                name: "IX_Flags_ClearedBy",
                table: "Flags",
                newName: "IX_Flags_clearedBy");

            migrationBuilder.RenameColumn(
                name: "TicketId",
                table: "Decisions",
                newName: "ticketId");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "Decisions",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "DecisionType",
                table: "Decisions",
                newName: "decisionType");

            migrationBuilder.RenameColumn(
                name: "DecidedBy",
                table: "Decisions",
                newName: "decidedBy");

            migrationBuilder.RenameColumn(
                name: "DecidedAt",
                table: "Decisions",
                newName: "decidedAt");

            migrationBuilder.RenameColumn(
                name: "DecisionId",
                table: "Decisions",
                newName: "decisionId");

            migrationBuilder.RenameIndex(
                name: "IX_Decisions_DecidedBy",
                table: "Decisions",
                newName: "IX_Decisions_decidedBy");

            migrationBuilder.RenameColumn(
                name: "TaxIdType",
                table: "Customers",
                newName: "taxIdType");

            migrationBuilder.RenameColumn(
                name: "TaxIdLastFourDigits",
                table: "Customers",
                newName: "taxIdLastFourDigits");

            migrationBuilder.RenameColumn(
                name: "TaxIdHash",
                table: "Customers",
                newName: "taxIdHash");

            migrationBuilder.RenameColumn(
                name: "ResidentialAddress",
                table: "Customers",
                newName: "residentialAddress");

            migrationBuilder.RenameColumn(
                name: "Occupation",
                table: "Customers",
                newName: "occupation");

            migrationBuilder.RenameColumn(
                name: "MiddleName",
                table: "Customers",
                newName: "middleName");

            migrationBuilder.RenameColumn(
                name: "LastName",
                table: "Customers",
                newName: "lastName");

            migrationBuilder.RenameColumn(
                name: "Gender",
                table: "Customers",
                newName: "gender");

            migrationBuilder.RenameColumn(
                name: "FirstName",
                table: "Customers",
                newName: "firstName");

            migrationBuilder.RenameColumn(
                name: "Employer",
                table: "Customers",
                newName: "employer");

            migrationBuilder.RenameColumn(
                name: "DateOfBirth",
                table: "Customers",
                newName: "dateOfBirth");

            migrationBuilder.RenameColumn(
                name: "CountryOfResidence",
                table: "Customers",
                newName: "countryOfResidence");

            migrationBuilder.RenameColumn(
                name: "CountryOfCitizenship",
                table: "Customers",
                newName: "countryOfCitizenship");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "Customers",
                newName: "customerId");

            migrationBuilder.RenameColumn(
                name: "TicketId",
                table: "AuditTrails",
                newName: "ticketId");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "AuditTrails",
                newName: "reason");

            migrationBuilder.RenameColumn(
                name: "PrevStatus",
                table: "AuditTrails",
                newName: "prevStatus");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "AuditTrails",
                newName: "message");

            migrationBuilder.RenameColumn(
                name: "DateTimeOccurred",
                table: "AuditTrails",
                newName: "dateTimeOccurred");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "AuditTrails",
                newName: "customerId");

            migrationBuilder.RenameColumn(
                name: "CurrStatus",
                table: "AuditTrails",
                newName: "currStatus");

            migrationBuilder.RenameColumn(
                name: "RecId",
                table: "AuditTrails",
                newName: "recId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditTrails_TicketId",
                table: "AuditTrails",
                newName: "IX_AuditTrails_ticketId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditTrails_CustomerId",
                table: "AuditTrails",
                newName: "IX_AuditTrails_customerId");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Admins",
                newName: "username");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "Admins",
                newName: "passwordhash");

            migrationBuilder.RenameColumn(
                name: "AdminId",
                table: "Admins",
                newName: "AdminID");

            migrationBuilder.AlterColumn<string>(
                name: "passwordhash",
                table: "Admins",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Admins_username",
                table: "Admins",
                column: "username");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AddressMatch",
                table: "VendorResponses",
                sql: "addressMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DateOfBirthMatch",
                table: "VendorResponses",
                sql: "dateOfBirthMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NameMatch",
                table: "VendorResponses",
                sql: "nameMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Score",
                table: "VendorResponses",
                sql: "score BETWEEN 0 AND 50");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxIdMatch",
                table: "VendorResponses",
                sql: "taxIdMatch IN ('Match', 'Partial', 'NoMatch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountType",
                table: "Tickets",
                sql: "accountType IN ('Individual', 'Joint')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RiskRating",
                table: "Tickets",
                sql: "riskRating IS NULL OR riskRating IN ('Low', 'Medium', 'High')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Status",
                table: "Tickets",
                sql: "status IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VerificationDeadline",
                table: "Tickets",
                sql: "verificationDeadline = DATEADD(DAY, 60, createdAt)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ViolationType",
                table: "Flags",
                sql: "violationType IN ('VerificationFailed', 'OfacMatch', 'ProhibitedIndustry', 'ClosedJurisdiction')");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_ticketId",
                table: "Decisions",
                column: "ticketId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DecisionReason",
                table: "Decisions",
                sql: "reason IS NOT NULL AND LEN(reason) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions",
                sql: "decisionType IN ('Approve', 'Deny')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Gender",
                table: "Customers",
                sql: "gender IN ('Male', 'Female', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ResidentialAddress",
                table: "Customers",
                sql: "LEN(residentialAddress) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxIdLastFourDigits",
                table: "Customers",
                sql: "LEN(taxIdLastFourDigits) = 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaxIdType",
                table: "Customers",
                sql: "taxIdType IN ('SSN', 'ITIN', 'EIN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails",
                sql: "currStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails",
                sql: "prevStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrails_Customers_customerId",
                table: "AuditTrails",
                column: "customerId",
                principalTable: "Customers",
                principalColumn: "customerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrails_Tickets_ticketId",
                table: "AuditTrails",
                column: "ticketId",
                principalTable: "Tickets",
                principalColumn: "ticketId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Decisions_Admins_decidedBy",
                table: "Decisions",
                column: "decidedBy",
                principalTable: "Admins",
                principalColumn: "username",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Decisions_Tickets_ticketId",
                table: "Decisions",
                column: "ticketId",
                principalTable: "Tickets",
                principalColumn: "ticketId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flags_Admins_clearedBy",
                table: "Flags",
                column: "clearedBy",
                principalTable: "Admins",
                principalColumn: "AdminID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flags_Admins_raisedBy",
                table: "Flags",
                column: "raisedBy",
                principalTable: "Admins",
                principalColumn: "AdminID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Flags_Tickets_ticketId",
                table: "Flags",
                column: "ticketId",
                principalTable: "Tickets",
                principalColumn: "ticketId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Admins_assignedTo",
                table: "Tickets",
                column: "assignedTo",
                principalTable: "Admins",
                principalColumn: "username",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Customers_customerId",
                table: "Tickets",
                column: "customerId",
                principalTable: "Customers",
                principalColumn: "customerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VendorResponses_Tickets_ticketId",
                table: "VendorResponses",
                column: "ticketId",
                principalTable: "Tickets",
                principalColumn: "ticketId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
