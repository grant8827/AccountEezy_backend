using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageSalePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MonthlySaleEnabled",
                table: "SubscriptionPackages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MonthlySalePriceJmd",
                table: "SubscriptionPackages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "YearlySaleEnabled",
                table: "SubscriptionPackages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "YearlySalePriceJmd",
                table: "SubscriptionPackages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "YearlyPriceJmd",
                table: "SubscriptionPackages",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlySaleEnabled",
                table: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "MonthlySalePriceJmd",
                table: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "YearlySaleEnabled",
                table: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "YearlySalePriceJmd",
                table: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "YearlyPriceJmd",
                table: "SubscriptionPackages");
        }
    }
}
