using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginLockoutRecoveryAndAdminProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Customers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "Customers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Customers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordRecoveryAnswer",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordRecoveryQuestion",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Customers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CountryOfCitizenship",
                table: "Admins",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateHired",
                table: "Admins",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Admins",
                type: "date",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Admins",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Admins",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Admins",
                type: "varchar(6)",
                maxLength: 6,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsCitizen",
                table: "Admins",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Admins",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "Admins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "Admins",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordRecoveryAnswer",
                table: "Admins",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PasswordRecoveryQuestion",
                table: "Admins",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "SSNHash",
                table: "Admins",
                type: "binary(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: new byte[0]);

            // Rows that existed before this migration get placeholder values so the unique indexes and the gender check can be created.
            // The placeholder password and recovery answer hashes ("!") never verify, so these legacy accounts can't be logged into
            // or recovered until someone sets real values; existing admins keep their password hash and can still log in.
            migrationBuilder.Sql(
                "UPDATE Customers SET Username = CONCAT('legacy_customer_', CustomerId), PasswordHash = '!', " +
                "PasswordRecoveryQuestion = 'What is your favorite food?', PasswordRecoveryAnswer = '!'");
            migrationBuilder.Sql(
                "UPDATE Admins SET FirstName = 'Legacy', LastName = 'Admin', DateOfBirth = '1900-01-01', Gender = 'Other', " +
                "SSNHash = HASHBYTES('SHA2_256', CONCAT('legacy-admin-', AdminId)), IsCitizen = 1, CountryOfCitizenship = 'US', " +
                "PasswordRecoveryQuestion = 'What is your favorite food?', PasswordRecoveryAnswer = '!'");

            migrationBuilder.CreateIndex(
                name: "UX_Customers_Username",
                table: "Customers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Admins_SSNHash",
                table: "Admins",
                column: "SSNHash",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Admins_Gender",
                table: "Admins",
                sql: "Gender IN ('Male', 'Female', 'Other')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Customers_Username",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "UX_Admins_SSNHash",
                table: "Admins");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Admins_Gender",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PasswordRecoveryAnswer",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PasswordRecoveryQuestion",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CountryOfCitizenship",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "DateHired",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "IsCitizen",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "PasswordRecoveryAnswer",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "PasswordRecoveryQuestion",
                table: "Admins");

            migrationBuilder.DropColumn(
                name: "SSNHash",
                table: "Admins");
        }
    }
}
