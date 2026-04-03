namespace backend.DTOs.Reports;

public class FinancialLineItem
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

public class FinancialSummary
{
    public List<FinancialLineItem> IncomeItems  { get; set; } = new();
    public List<FinancialLineItem> ExpenseItems { get; set; } = new();
    public decimal TotalIncome     { get; set; }
    public decimal TotalExpenses   { get; set; }
    public decimal TotalSalaryPaid { get; set; }
    public decimal NetPosition     { get; set; }  // TotalIncome - TotalExpenses - TotalSalaryPaid
}
