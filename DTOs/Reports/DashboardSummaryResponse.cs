namespace backend.DTOs.Reports;

public class DashboardSummaryResponse
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal GctLiability { get; set; }
    public decimal PayrollTaxLiability { get; set; }
    public decimal TotalTaxLiability { get; set; }
    public decimal CashFlow { get; set; }
}
