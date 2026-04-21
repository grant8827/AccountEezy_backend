using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessEmail",
                table: "Businesses",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPhone",
                table: "Businesses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "Businesses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Businesses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Businesses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Businesses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FiscalYearEnd",
                table: "Businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Businesses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NIS",
                table: "Businesses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Parish",
                table: "Businesses",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Businesses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Businesses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationNumber",
                table: "Businesses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Street",
                table: "Businesses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Businesses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessEmail",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "BusinessPhone",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "FiscalYearEnd",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "NIS",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Parish",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "RegistrationNumber",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Street",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Businesses");
        }
    }
}
