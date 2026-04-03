namespace backend.DTOs.Reports;

public class So2ReportResponse
{
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string TRN { get; set; } = string.Empty;
    public int Year { get; set; }
    public int EmployeeCount { get; set; }

    // Annual aggregates
    public decimal TotalNisEmployee { get; set; }
    public decimal TotalNisEmployer { get; set; }
    public decimal TotalNhtEmployee { get; set; }
    public decimal TotalNhtEmployer { get; set; }
    public decimal TotalEducationTaxEmployee { get; set; }
    public decimal TotalEducationTaxEmployer { get; set; }
    public decimal TotalPayeEmployee { get; set; }
    public decimal TotalHeartEmployer { get; set; }

    // Summary totals
    public decimal TotalPayrollRemittance { get; set; }
    public decimal TotalGctPayable { get; set; }
    public decimal TotalAnnualRemittance { get; set; }

    // Month-by-month for the annual breakdown table
    public List<So2MonthRow> MonthlyBreakdown { get; set; } = new();
}

public class So2MonthRow
{
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal PayrollRemittance { get; set; }
    public decimal GctPayable { get; set; }
    public decimal TotalRemittance { get; set; }
    public string Status { get; set; } = "Pending";
}
