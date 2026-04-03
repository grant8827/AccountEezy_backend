namespace backend.DTOs.Payroll;

public class PayrollResponse
{
    // Earnings breakdown
    public decimal BaseSalary { get; set; }
    public decimal HolidayPay { get; set; }
    public decimal Bonus { get; set; }
    public decimal GrossMonthlySalary { get; set; }

    // Employee deductions
    public decimal EmployeeNis { get; set; }
    public decimal EmployeeNht { get; set; }
    public decimal EmployeeEducationTax { get; set; }
    public decimal EmployeePaye { get; set; }
    public decimal LoanDeduction { get; set; }

    // Employer contributions
    public decimal EmployerNis { get; set; }
    public decimal EmployerNht { get; set; }
    public decimal EmployerEducationTax { get; set; }
    public decimal EmployerHeart { get; set; }

    // Totals
    public decimal ConsolidatedPayrollTaxEmployee { get; set; }
    public decimal ConsolidatedPayrollTaxEmployer { get; set; }
    public decimal TotalStatutoryRemittance { get; set; }
    public decimal NetMonthlySalary { get; set; }
}
