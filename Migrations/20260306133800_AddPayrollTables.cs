using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    PayCycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Label = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBatches_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaxConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BusinessId = table.Column<int>(type: "integer", nullable: false),
                    NisRateEmployee = table.Column<decimal>(type: "numeric", nullable: false),
                    NhtRateEmployee = table.Column<decimal>(type: "numeric", nullable: false),
                    EducationTaxRateEmployee = table.Column<decimal>(type: "numeric", nullable: false),
                    PayeRateLower = table.Column<decimal>(type: "numeric", nullable: false),
                    PayeRateUpper = table.Column<decimal>(type: "numeric", nullable: false),
                    NisRateEmployer = table.Column<decimal>(type: "numeric", nullable: false),
                    NhtRateEmployer = table.Column<decimal>(type: "numeric", nullable: false),
                    EducationTaxRateEmployer = table.Column<decimal>(type: "numeric", nullable: false),
                    HeartRateEmployer = table.Column<decimal>(type: "numeric", nullable: false),
                    IncomeTaxThresholdAnnual = table.Column<decimal>(type: "numeric", nullable: false),
                    PayeUpperBandAnnual = table.Column<decimal>(type: "numeric", nullable: false),
                    NisAnnualCeiling = table.Column<decimal>(type: "numeric", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxConfigurations_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayrollBatchId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "numeric", nullable: false),
                    HolidayPay = table.Column<decimal>(type: "numeric", nullable: false),
                    Bonus = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossPay = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployeeNis = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployeeNht = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployeeEducationTax = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployeePaye = table.Column<decimal>(type: "numeric", nullable: false),
                    LoanDeduction = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerNis = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerNht = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerEducationTax = table.Column<decimal>(type: "numeric", nullable: false),
                    EmployerHeart = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalStatutoryDeductions = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric", nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollEntries_PayrollBatches_PayrollBatchId",
                        column: x => x.PayrollBatchId,
                        principalTable: "PayrollBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_BusinessId",
                table: "PayrollBatches",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_EmployeeId",
                table: "PayrollEntries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollEntries_PayrollBatchId",
                table: "PayrollEntries",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxConfigurations_BusinessId",
                table: "TaxConfigurations",
                column: "BusinessId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollEntries");

            migrationBuilder.DropTable(
                name: "TaxConfigurations");

            migrationBuilder.DropTable(
                name: "PayrollBatches");
        }
    }
}
