using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFormatVerificationAndRenames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Status",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails");

            migrationBuilder.RenameColumn(
                name: "TimeStamp",
                table: "VendorResponses",
                newName: "VRTimeStamp");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "AuditTrails",
                newName: "ATMessage");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Status",
                table: "Tickets",
                sql: "Status IN ('Submitted', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions",
                sql: "DecisionType IN ('Approve', 'Reject')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails",
                sql: "CurrStatus IN ('Submitted', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails",
                sql: "PrevStatus IN ('Submitted', 'Review', 'Approved', 'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Status",
                table: "Tickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails");

            migrationBuilder.RenameColumn(
                name: "VRTimeStamp",
                table: "VendorResponses",
                newName: "TimeStamp");

            migrationBuilder.RenameColumn(
                name: "ATMessage",
                table: "AuditTrails",
                newName: "Message");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Status",
                table: "Tickets",
                sql: "Status IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DecisionType",
                table: "Decisions",
                sql: "DecisionType IN ('Approve', 'Deny')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditCurrStatus",
                table: "AuditTrails",
                sql: "CurrStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AuditPrevStatus",
                table: "AuditTrails",
                sql: "PrevStatus IN ('Submitted', 'FormatVerification', 'Review', 'Approved', 'Rejected')");
        }
    }
}
