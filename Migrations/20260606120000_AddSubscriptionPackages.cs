using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    [Migration("20260606120000_AddSubscriptionPackages")]
    public partial class AddSubscriptionPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MonthlyPriceJmd = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCustom = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPackages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPackages_Key",
                table: "SubscriptionPackages",
                column: "Key",
                unique: true);

            migrationBuilder.InsertData(
                table: "SubscriptionPackages",
                columns: new[] { "Key", "Name", "MonthlyPriceJmd", "DisplayOrder", "IsCustom", "DiscountEnabled", "DiscountPercent", "UpdatedAt" },
                values: new object[,]
                {
                    { "lite", "Lite", 3500L, 1, false, false, 0m, DateTime.UtcNow },
                    { "starter", "Starter", 6500L, 2, false, false, 0m, DateTime.UtcNow },
                    { "growth", "Growth", 12500L, 3, false, false, 0m, DateTime.UtcNow },
                    { "custom", "Custom", 15000L, 4, true, false, 0m, DateTime.UtcNow }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionPackages");
        }
    }
}
