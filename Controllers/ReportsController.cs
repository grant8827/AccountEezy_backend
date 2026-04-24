using System.Security.Claims;
using backend.Data;
using backend.DTOs.Reports;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReportsController(AppDbContext dbContext, IPayrollService payrollService) : ControllerBase
{
    // ── Legacy SO1 – kept for backward compatibility ───────────────────────────
    [HttpGet("so1")]
    public async Task<ActionResult<So1ReportResponse>> GetSo1([FromQuery] int month, [FromQuery] int year)
    {
        if (month is < 1 or > 12) return BadRequest("Month must be 1-12.");
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var business = await dbContext.Businesses.FindAsync(businessId.Value);
        if (business is null) return NotFound();

        var report = await BuildMonthlyReport(businessId.Value, month, year);

        var employeeCount = await dbContext.Employees
            .CountAsync(e => e.BusinessId == businessId.Value);

        // Fetch per-employee deductions from processed payroll entries for this month
        var payrollEntries = await dbContext.PayrollEntries
            .Include(e => e.Employee)
            .Include(e => e.Batch)
            .Where(e => e.Batch!.BusinessId == businessId.Value
                     && e.Batch.Status != PayrollBatchStatus.Draft
                     && (e.Batch.PayDate ?? e.Batch.EndDate).Month == month
                     && (e.Batch.PayDate ?? e.Batch.EndDate).Year == year)
            .OrderBy(e => e.Employee!.Name)
            .ToListAsync();

        var employeeDeductions = payrollEntries.Select(e => new EmployeeDeductionRow
        {
            EmployeeName    = e.Employee?.Name ?? "Unknown",
            Nis             = e.EmployeeNis,
            Nht             = e.EmployeeNht,
            EducationTax    = e.EmployeeEducationTax,
            Paye            = e.EmployeePaye,
            LoanDeduction   = e.LoanDeduction,
            TotalDeductions = e.TotalDeductions
        }).ToList();

        return Ok(new So1ReportResponse
        {
            BusinessId           = businessId.Value,
            BusinessName         = business.CompanyName,
            TRN                  = business.TRN,
            Month                = month,
            MonthName            = new DateTime(year, month, 1).ToString("MMMM"),
            Year                 = year,
            EmployeeCount        = employeeCount,
            NisEmployee          = report.NisEmployee,
            NisEmployer          = report.NisEmployer,
            NhtEmployee          = report.NhtEmployee,
            NhtEmployer          = report.NhtEmployer,
            EducationTaxEmployee = report.EducationTaxEmployee,
            EducationTaxEmployer = report.EducationTaxEmployer,
            PayeEmployee         = report.PayeEmployee,
            HeartEmployer        = report.HeartEmployer,
            PayrollRemittance    = report.TotalPayrollRemittance,
            GctPayable           = report.GctPayable,
            TotalRemittance      = report.TotalRemittance,
            Status               = report.Status,
            TaxRecordId          = report.TaxRecordId,
            Financial            = report.Financial,
            EmployeeDeductions   = employeeDeductions
        });
    }

    // ── Legacy SO2 – kept for backward compatibility ───────────────────────────
    [HttpGet("so2")]
    public async Task<ActionResult<So2ReportResponse>> GetSo2([FromQuery] int year)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var business = await dbContext.Businesses.FindAsync(businessId.Value);
        if (business is null) return NotFound();

        var yearly = await BuildYearlyReport(businessId.Value, year);

        var employeeCount = await dbContext.Employees
            .CountAsync(e => e.BusinessId == businessId.Value);

        // Only include months where an SO1 was generated (i.e. a processed/paid payroll batch exists)
        var monthsWithPayroll = await dbContext.PayrollEntries
            .Include(e => e.Batch)
            .Where(e => e.Batch!.BusinessId == businessId.Value
                     && e.Batch.Status != PayrollBatchStatus.Draft
                     && (e.Batch.PayDate ?? e.Batch.EndDate).Year == year)
            .Select(e => (e.Batch!.PayDate ?? e.Batch.EndDate).Month)
            .Distinct()
            .ToListAsync();

        // Filter yearly months to only those with processed payroll
        var payrollMonths = yearly.Months
            .Where(m => monthsWithPayroll.Contains(m.Month))
            .ToList();

        var monthlyBreakdown = payrollMonths
            .Select(m => new So2MonthRow
            {
                Month             = m.Month,
                MonthName         = m.MonthName,
                PayrollRemittance = m.TotalPayrollRemittance,
                GctPayable        = m.GctPayable,
                TotalRemittance   = m.TotalRemittance,
                Status            = m.Status
            }).ToList();

        return Ok(new So2ReportResponse
        {
            BusinessId                = businessId.Value,
            BusinessName              = business.CompanyName,
            TRN                       = business.TRN,
            Year                      = year,
            EmployeeCount             = employeeCount,
            TotalNisEmployee          = Round2(payrollMonths.Sum(m => m.NisEmployee)),
            TotalNisEmployer          = Round2(payrollMonths.Sum(m => m.NisEmployer)),
            TotalNhtEmployee          = Round2(payrollMonths.Sum(m => m.NhtEmployee)),
            TotalNhtEmployer          = Round2(payrollMonths.Sum(m => m.NhtEmployer)),
            TotalEducationTaxEmployee = Round2(payrollMonths.Sum(m => m.EducationTaxEmployee)),
            TotalEducationTaxEmployer = Round2(payrollMonths.Sum(m => m.EducationTaxEmployer)),
            TotalPayeEmployee         = Round2(payrollMonths.Sum(m => m.PayeEmployee)),
            TotalHeartEmployer        = Round2(payrollMonths.Sum(m => m.HeartEmployer)),
            TotalPayrollRemittance    = Round2(payrollMonths.Sum(m => m.TotalPayrollRemittance)),
            TotalGctPayable           = Round2(payrollMonths.Sum(m => m.GctPayable)),
            TotalAnnualRemittance     = Round2(payrollMonths.Sum(m => m.TotalRemittance)),
            MonthlyBreakdown          = monthlyBreakdown
        });
    }

    // ── Monthly Tax Report ─────────────────────────────────────────────────────
    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyTaxReportResponse>> GetMonthly([FromQuery] int month, [FromQuery] int year)
    {
        if (month is < 1 or > 12) return BadRequest("Month must be 1-12.");
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        return Ok(await BuildMonthlyReport(businessId.Value, month, year));
    }

    // ── Quarterly Tax Report ───────────────────────────────────────────────────
    [HttpGet("quarterly")]
    public async Task<ActionResult<QuarterlyTaxReportResponse>> GetQuarterly([FromQuery] int quarter, [FromQuery] int year)
    {
        if (quarter is < 1 or > 4) return BadRequest("Quarter must be 1-4.");
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var startMonth = (quarter - 1) * 3 + 1;
        var endMonth   = startMonth + 2;
        var months = new List<MonthlyTaxReportResponse>();
        for (var m = startMonth; m <= endMonth; m++)
            months.Add(await BuildMonthlyReport(businessId.Value, m, year));

        var quarterFrom = new DateTime(year, startMonth, 1);
        var quarterTo   = new DateTime(year, endMonth, DateTime.DaysInMonth(year, endMonth));

        return Ok(new QuarterlyTaxReportResponse
        {
            BusinessId = businessId.Value,
            Quarter = quarter,
            Year = year,
            QuarterLabel = $"Q{quarter} {year}",
            Months = months,
            TotalNisEmployee         = Round2(months.Sum(x => x.NisEmployee)),
            TotalNisEmployer         = Round2(months.Sum(x => x.NisEmployer)),
            TotalNhtEmployee         = Round2(months.Sum(x => x.NhtEmployee)),
            TotalNhtEmployer         = Round2(months.Sum(x => x.NhtEmployer)),
            TotalEducationTaxEmployee = Round2(months.Sum(x => x.EducationTaxEmployee)),
            TotalEducationTaxEmployer = Round2(months.Sum(x => x.EducationTaxEmployer)),
            TotalPayeEmployee        = Round2(months.Sum(x => x.PayeEmployee)),
            TotalHeartEmployer       = Round2(months.Sum(x => x.HeartEmployer)),
            TotalGctPayable          = Round2(months.Sum(x => x.GctPayable)),
            TotalPayrollRemittance   = Round2(months.Sum(x => x.TotalPayrollRemittance)),
            TotalRemittance          = Round2(months.Sum(x => x.TotalRemittance)),
            Financial                = await BuildFinancialSummary(businessId.Value, quarterFrom, quarterTo)
        });
    }

    // ── Yearly Tax Report ──────────────────────────────────────────────────────
    [HttpGet("yearly")]
    public async Task<ActionResult<YearlyTaxReportResponse>> GetYearly([FromQuery] int year)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        return Ok(await BuildYearlyReport(businessId.Value, year));
    }

    // ── Tax Record History ─────────────────────────────────────────────────────
    [HttpGet("tax-history")]
    public async Task<ActionResult<IEnumerable<TaxRecord>>> GetTaxHistory()
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var records = await dbContext.TaxRecords
            .Where(t => t.BusinessId == businessId.Value)
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.Month)
            .ToListAsync();

        return Ok(records);
    }

    // ── Mark Tax Record as Paid ────────────────────────────────────────────────
    [HttpPatch("tax-records/{id}/pay")]
    public async Task<ActionResult> MarkAsPaid(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var record = await dbContext.TaxRecords
            .FirstOrDefaultAsync(t => t.Id == id && t.BusinessId == businessId.Value);

        if (record is null) return NotFound();

        record.Status = TaxRecordStatus.Paid;
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    // ── Shared Builders ────────────────────────────────────────────────────────
    private async Task<MonthlyTaxReportResponse> BuildMonthlyReport(int businessId, int month, int year)
    {
        // Prefer data from processed/paid payroll batches for accuracy
        var entries = await dbContext.PayrollEntries
            .Include(e => e.Batch)
            .Where(e => e.Batch!.BusinessId == businessId
                     && e.Batch.Status != PayrollBatchStatus.Draft
                     && (e.Batch.PayDate ?? e.Batch.EndDate).Month == month
                     && (e.Batch.PayDate ?? e.Batch.EndDate).Year == year)
            .ToListAsync();

        decimal nisEmployee, nisEmployer, nhtEmployee, nhtEmployer,
                edTaxEmployee, edTaxEmployer, paye, heart;

        if (entries.Count > 0)
        {
            nisEmployee   = Round2(entries.Sum(e => e.EmployeeNis));
            nisEmployer   = Round2(entries.Sum(e => e.EmployerNis));
            nhtEmployee   = Round2(entries.Sum(e => e.EmployeeNht));
            nhtEmployer   = Round2(entries.Sum(e => e.EmployerNht));
            edTaxEmployee = Round2(entries.Sum(e => e.EmployeeEducationTax));
            edTaxEmployer = Round2(entries.Sum(e => e.EmployerEducationTax));
            paye          = Round2(entries.Sum(e => e.EmployeePaye));
            heart         = Round2(entries.Sum(e => e.EmployerHeart));
        }
        else
        {
            // No processed payroll for this month — all payroll deductions are zero
            nisEmployee   = 0;
            nisEmployer   = 0;
            nhtEmployee   = 0;
            nhtEmployer   = 0;
            edTaxEmployee = 0;
            edTaxEmployer = 0;
            paye          = 0;
            heart         = 0;
        }

        var gctPayable = Round2(await dbContext.Transactions
            .Where(t => t.BusinessId == businessId && t.Date.Month == month && t.Date.Year == year)
            .SumAsync(t => t.GctAmount));

        var totalPayroll    = Round2(nisEmployee + nisEmployer + nhtEmployee + nhtEmployer
                                   + edTaxEmployee + edTaxEmployer + paye + heart);
        var totalRemittance = Round2(totalPayroll + gctPayable);

        // Upsert TaxRecord
        var record = await dbContext.TaxRecords
            .FirstOrDefaultAsync(t => t.BusinessId == businessId && t.Month == month && t.Year == year);

        if (record is null)
        {
            record = new TaxRecord
            {
                BusinessId = businessId,
                Month      = month,
                Year       = year,
                Status     = TaxRecordStatus.Pending
            };
            dbContext.TaxRecords.Add(record);
        }
        record.TotalRemittance = totalRemittance;
        await dbContext.SaveChangesAsync();

        return new MonthlyTaxReportResponse
        {
            BusinessId            = businessId,
            Month                 = month,
            Year                  = year,
            MonthName             = new DateTime(year, month, 1).ToString("MMMM"),
            NisEmployee           = nisEmployee,
            NisEmployer           = nisEmployer,
            NhtEmployee           = nhtEmployee,
            NhtEmployer           = nhtEmployer,
            EducationTaxEmployee  = edTaxEmployee,
            EducationTaxEmployer  = edTaxEmployer,
            PayeEmployee          = paye,
            HeartEmployer         = heart,
            GctPayable            = gctPayable,
            TotalPayrollRemittance = totalPayroll,
            TotalGct              = gctPayable,
            TotalRemittance       = totalRemittance,
            Status                = record.Status.ToString(),
            TaxRecordId           = record.Id,
            Financial             = await BuildFinancialSummary(businessId,
                                        new DateTime(year, month, 1),
                                        new DateTime(year, month, DateTime.DaysInMonth(year, month)))
        };
    }

    private async Task<YearlyTaxReportResponse> BuildYearlyReport(int businessId, int year)
    {
        var months = new List<MonthlyTaxReportResponse>();
        for (var m = 1; m <= 12; m++)
            months.Add(await BuildMonthlyReport(businessId, m, year));

        return new YearlyTaxReportResponse
        {
            BusinessId               = businessId,
            Year                     = year,
            Months                   = months,
            TotalNisEmployee         = Round2(months.Sum(x => x.NisEmployee)),
            TotalNisEmployer         = Round2(months.Sum(x => x.NisEmployer)),
            TotalNhtEmployee         = Round2(months.Sum(x => x.NhtEmployee)),
            TotalNhtEmployer         = Round2(months.Sum(x => x.NhtEmployer)),
            TotalEducationTaxEmployee = Round2(months.Sum(x => x.EducationTaxEmployee)),
            TotalEducationTaxEmployer = Round2(months.Sum(x => x.EducationTaxEmployer)),
            TotalPayeEmployee        = Round2(months.Sum(x => x.PayeEmployee)),
            TotalHeartEmployer       = Round2(months.Sum(x => x.HeartEmployer)),
            TotalGctPayable          = Round2(months.Sum(x => x.GctPayable)),
            TotalPayrollRemittance   = Round2(months.Sum(x => x.TotalPayrollRemittance)),
            TotalRemittance          = Round2(months.Sum(x => x.TotalRemittance)),
            Financial                = await BuildFinancialSummary(businessId,
                                           new DateTime(year, 1, 1),
                                           new DateTime(year, 12, 31))
        };
    }

    private async Task<FinancialSummary> BuildFinancialSummary(int businessId, DateTime from, DateTime to)
    {
        // Npgsql v6+ requires DateTimeKind.Utc for timestamptz columns.
        // Use an exclusive upper bound (start of the next day) rather than .Date property.
        var fromUtc   = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var toUtcEnd  = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Utc);

        var transactions = await dbContext.Transactions
            .Where(t => t.BusinessId == businessId && t.Date >= fromUtc && t.Date < toUtcEnd)
            .ToListAsync();

        var incomeItems = transactions
            .Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => t.Category)
            .Select(g => new FinancialLineItem { Category = g.Key, TotalAmount = Round2(g.Sum(t => t.Amount)) })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var expenseItems = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Category)
            .Select(g => new FinancialLineItem { Category = g.Key, TotalAmount = Round2(g.Sum(t => t.Amount)) })
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        var totalIncome   = Round2(incomeItems.Sum(x => x.TotalAmount));
        var totalExpenses = Round2(expenseItems.Sum(x => x.TotalAmount));

        // Salary paid: sum net pay from processed/paid batches in the period
        var salaryPaid = Round2(await dbContext.PayrollEntries
            .Where(e => e.Batch!.BusinessId == businessId
                     && e.Batch.Status != PayrollBatchStatus.Draft
                     && e.Batch.StartDate >= fromUtc
                     && e.Batch.StartDate < toUtcEnd)
            .SumAsync(e => e.NetPay));

        return new FinancialSummary
        {
            IncomeItems     = incomeItems,
            ExpenseItems    = expenseItems,
            TotalIncome     = totalIncome,
            TotalExpenses   = totalExpenses,
            TotalSalaryPaid = salaryPaid,
            NetPosition     = Round2(totalIncome - totalExpenses - salaryPaid)
        };
    }

    private int? GetBusinessId()
    {
        var claim = User.FindFirstValue("businessId") ?? User.FindFirstValue(ClaimTypes.GroupSid);
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
