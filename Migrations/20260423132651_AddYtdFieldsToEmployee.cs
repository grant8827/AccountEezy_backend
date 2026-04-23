using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddYtdFieldsToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "YtdEducationTax",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdGross",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdNht",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdNis",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdPaye",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "YtdTotalDeductions",
                table: "Employees",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "YtdEducationTax",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "YtdGross",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "YtdNht",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "YtdNis",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "YtdPaye",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "YtdTotalDeductions",
                table: "Employees");
        }
    }
}
