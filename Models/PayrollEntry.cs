namespace backend.Models;

public class PayrollEntry
{
    public int Id { get; set; }

    public int PayrollBatchId { get; set; }
    public PayrollBatch? Batch { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    // ── Earnings ──────────────────────────────────────────────────────────────
    public decimal BaseSalary { get; set; }
    public decimal HolidayPay { get; set; } = 0m;
    public decimal Bonus { get; set; } = 0m;
    public decimal GrossPay { get; set; }      // BaseSalary + HolidayPay + Bonus

    // ── Employee Statutory Deductions ─────────────────────────────────────────
    public decimal EmployeeNis { get; set; }
    public decimal EmployeeNht { get; set; }
    public decimal EmployeeEducationTax { get; set; }
    public decimal EmployeePaye { get; set; }

    // ── Other Deductions ─────────────────────────────────────────────────────
    public decimal LoanDeduction { get; set; } = 0m;

    // ── Employer Contributions ───────────────────────────────────────────────
    public decimal EmployerNis { get; set; }
    public decimal EmployerNht { get; set; }
    public decimal EmployerEducationTax { get; set; }
    public decimal EmployerHeart { get; set; }

    // ── Totals ────────────────────────────────────────────────────────────────
    public decimal TotalStatutoryDeductions { get; set; }
    public decimal TotalDeductions { get; set; }        // statutory + loan
    public decimal NetPay { get; set; }
}
