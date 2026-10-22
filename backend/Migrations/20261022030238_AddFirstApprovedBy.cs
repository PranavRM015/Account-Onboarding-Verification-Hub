using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstApprovedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstApprovedBy",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FirstApprovedBy",
                table: "Tickets",
                column: "FirstApprovedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_Admins_FirstApprovedBy",
                table: "Tickets",
                column: "FirstApprovedBy",
                principalTable: "Admins",
                principalColumn: "Username",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Admins_FirstApprovedBy",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_FirstApprovedBy",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "FirstApprovedBy",
                table: "Tickets");
        }
    }
}
