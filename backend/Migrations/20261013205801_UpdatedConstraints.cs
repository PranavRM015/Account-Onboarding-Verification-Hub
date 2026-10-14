using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CK_ClearedReason is now dropped in addConstraints, before the clearedBy column type changes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
