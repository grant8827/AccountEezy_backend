namespace backend.DTOs.Reports;

public class So1ReportResponse
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string TRN { get; set; } = string.Empty;
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int EmployeeCount { get; set; }

    // Payroll breakdown
    public decimal NisEmployee { get; set; }
    public decimal NisEmployer { get; set; }
    public decimal NhtEmployee { get; set; }
    public decimal NhtEmployer { get; set; }
    public decimal EducationTaxEmployee { get; set; }
    public decimal EducationTaxEmployer { get; set; }
    public decimal PayeEmployee { get; set; }
    public decimal HeartEmployer { get; set; }

    // Totals
    public decimal PayrollRemittance { get; set; }
    public decimal GctPayable { get; set; }
    public decimal TotalRemittance { get; set; }

    // Status
    public string Status { get; set; } = "Pending";
    public int? TaxRecordId { get; set; }

    // Financial summary (income & expenses from transactions for the month)
    public FinancialSummary? Financial { get; set; }

    // Per-employee deductions from processed payroll batches
    public List<EmployeeDeductionRow> EmployeeDeductions { get; set; } = [];
}
