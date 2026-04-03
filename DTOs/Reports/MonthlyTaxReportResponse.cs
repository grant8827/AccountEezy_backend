namespace backend.DTOs.Reports;

public class MonthlyTaxReportResponse
{
    public int BusinessId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public string MonthName { get; set; } = string.Empty;

    // Payroll statutory components
    public decimal NisEmployee { get; set; }
    public decimal NisEmployer { get; set; }
    public decimal NhtEmployee { get; set; }
    public decimal NhtEmployer { get; set; }
    public decimal EducationTaxEmployee { get; set; }
    public decimal EducationTaxEmployer { get; set; }
    public decimal PayeEmployee { get; set; }
    public decimal HeartEmployer { get; set; }

    // GCT
    public decimal GctPayable { get; set; }

    // Totals
    public decimal TotalPayrollRemittance { get; set; }
    public decimal TotalGct { get; set; }
    public decimal TotalRemittance { get; set; }

    // Status
    public string Status { get; set; } = "Pending";
    public int? TaxRecordId { get; set; }

    // Financial summary (income / expenses / salary from transactions)
    public FinancialSummary Financial { get; set; } = new();
}
