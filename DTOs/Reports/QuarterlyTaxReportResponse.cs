namespace backend.DTOs.Reports;

public class QuarterlyTaxReportResponse
{
    public int BusinessId { get; set; }
    public int Quarter { get; set; }
    public int Year { get; set; }
    public string QuarterLabel { get; set; } = string.Empty;

    // Per-month breakdown
    public List<MonthlyTaxReportResponse> Months { get; set; } = new();

    // Aggregated totals
    public decimal TotalNisEmployee { get; set; }
    public decimal TotalNisEmployer { get; set; }
    public decimal TotalNhtEmployee { get; set; }
    public decimal TotalNhtEmployer { get; set; }
    public decimal TotalEducationTaxEmployee { get; set; }
    public decimal TotalEducationTaxEmployer { get; set; }
    public decimal TotalPayeEmployee { get; set; }
    public decimal TotalHeartEmployer { get; set; }
    public decimal TotalGctPayable { get; set; }
    public decimal TotalPayrollRemittance { get; set; }
    public decimal TotalRemittance { get; set; }

    // Financial summary (income / expenses / salary from transactions)
    public FinancialSummary Financial { get; set; } = new();
}
