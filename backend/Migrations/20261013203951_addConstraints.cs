using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class addConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CK_ClearedReason references Flags.clearedBy, so it has to be dropped before that column's type changes.
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClearedReason",
                table: "Flags");

            migrationBuilder.DropColumn(
                name: "userId",
                table: "AuditTrails");

            migrationBuilder.AlterColumn<string>(
                name: "assignedTo",
                table: "Tickets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "raisedBy",
                table: "Flags",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "clearedBy",
                table: "Flags",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "decidedBy",
                table: "Decisions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<int>(
                name: "customerId",
                table: "AuditTrails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "Admins",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Admins_username",
                table: "Admins",
                column: "username");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_assignedTo",
                table: "Tickets",
                column: "assignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_clearedBy",
                table: "Flags",
                column: "clearedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Flags_raisedBy",
                table: "Flags",
                column: "raisedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Decisions_decidedBy",
                table: "Decisions",
                column: "decidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_customerId",
                table: "AuditTrails",
                column: "customerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditTrails_Customers_customerId",
                table: "AuditTrails",
                column: "customerId",
                principalTable: "Customers",
                principalColumn: "customerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Decisions_Admins_decidedBy",
                table: "Decisions",
                column: "decidedBy",
                principalTable: "Admins",
                principalColumn: "username",
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
                name: "FK_Tickets_Admins_assignedTo",
                table: "Tickets",
                column: "assignedTo",
                principalTable: "Admins",
                principalColumn: "username",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditTrails_Customers_customerId",
                table: "AuditTrails");

            migrationBuilder.DropForeignKey(
                name: "FK_Decisions_Admins_decidedBy",
                table: "Decisions");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Admins_clearedBy",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Flags_Admins_raisedBy",
                table: "Flags");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_Admins_assignedTo",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_assignedTo",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Flags_clearedBy",
                table: "Flags");

            migrationBuilder.DropIndex(
                name: "IX_Flags_raisedBy",
                table: "Flags");

            migrationBuilder.DropIndex(
                name: "IX_Decisions_decidedBy",
                table: "Decisions");

            migrationBuilder.DropIndex(
                name: "IX_AuditTrails_customerId",
                table: "AuditTrails");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Admins_username",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "customerId",
                table: "AuditTrails");

            migrationBuilder.AlterColumn<string>(
                name: "assignedTo",
                table: "Tickets",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "raisedBy",
                table: "Flags",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "clearedBy",
                table: "Flags",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "decidedBy",
                table: "Decisions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "userId",
                table: "AuditTrails",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "Admins",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClearedReason",
                table: "Flags",
                sql: "clearedAt IS NULL OR (clearedBy IS NOT NULL AND clearedReason IS NOT NULL AND LEN(clearedReason) > 0)");
        }
    }
}
