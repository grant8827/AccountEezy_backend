namespace backend.DTOs.Reports;

public class EmployeeDeductionRow
{
    public string EmployeeName { get; set; } = string.Empty;
    public decimal Nis { get; set; }
    public decimal Nht { get; set; }
    public decimal EducationTax { get; set; }
    public decimal Paye { get; set; }
    public decimal LoanDeduction { get; set; }
    public decimal TotalDeductions { get; set; }
}
