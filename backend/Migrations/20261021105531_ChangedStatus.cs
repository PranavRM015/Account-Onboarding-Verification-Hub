using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChangedStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The check constraint references the column, which blocks sp_rename, so drop and recreate it.
            migrationBuilder.DropCheckConstraint(
                name: "CK_Status",
                table: "Tickets");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Tickets",
                newName: "TicketStatus");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Status",
                table: "Tickets",
                sql: "TicketStatus IN ('Submitted', 'Review', 'Approved', 'Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Status",
                table: "Tickets");

            migrationBuilder.RenameColumn(
                name: "TicketStatus",
                table: "Tickets",
                newName: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Status",
                table: "Tickets",
                sql: "Status IN ('Submitted', 'Review', 'Approved', 'Rejected')");
        }
    }
}
