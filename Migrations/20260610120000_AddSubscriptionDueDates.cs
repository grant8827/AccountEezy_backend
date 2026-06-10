using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    [Migration("20260610120000_AddSubscriptionDueDates")]
    public partial class AddSubscriptionDueDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionStartedAt",
                table: "Businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextPaymentDueAt",
                table: "Businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GracePeriodEndsAt",
                table: "Businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastPaymentMethod",
                table: "Businesses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionStartedAt",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "NextPaymentDueAt",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "GracePeriodEndsAt",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "LastPaymentMethod",
                table: "Businesses");
        }
    }
}
