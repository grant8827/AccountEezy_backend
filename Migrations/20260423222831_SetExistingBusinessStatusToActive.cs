using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class SetExistingBusinessStatusToActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set existing businesses with empty Status to "Active" so they aren't blocked
            migrationBuilder.Sql("UPDATE \"Businesses\" SET \"Status\" = 'Active' WHERE \"Status\" = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Businesses\" SET \"Status\" = '' WHERE \"Status\" = 'Active'");
        }
    }
}
