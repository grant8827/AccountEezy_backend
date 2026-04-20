using System.Security.Claims;
using backend.Data;
using backend.DTOs.Payroll;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public class CreateBatchRequest
{
    public required string PayCycle { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class BatchEntryInput
{
    public int EmployeeId { get; set; }
    public decimal HolidayPay { get; set; } = 0m;
    public decimal Bonus { get; set; } = 0m;
    public decimal LoanDeduction { get; set; } = 0m;
    public decimal? Hours { get; set; } // Required for hourly employees
}

public class ProcessBatchRequest
{
    public List<BatchEntryInput> Entries { get; set; } = new();
}

[ApiController]
[Authorize]
[Route("api/payroll-batches")]
public class PayrollBatchController(AppDbContext dbContext, IPayrollService payrollService) : ControllerBase
{
    // ── GET all batches ───────────────────────────────────────────────────────
    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var batches = await dbContext.PayrollBatches
            .Where(b => b.BusinessId == businessId.Value)
            .OrderByDescending(b => b.StartDate)
            .Select(b => new
            {
                b.Id, b.Label, b.PayCycle, b.StartDate, b.EndDate, b.Status, b.CreatedAt,
                EmployeeCount = b.Entries.Count,
                TotalNet = b.Entries.Sum(e => (decimal?)e.NetPay) ?? 0m,
                TotalRemittance = b.Entries.Sum(e => (decimal?)(e.EmployeeNis + e.EmployeeNht + e.EmployeeEducationTax + e.EmployeePaye
                                                   + e.EmployerNis + e.EmployerNht + e.EmployerEducationTax + e.EmployerHeart)) ?? 0m
            })
            .ToListAsync();

        return Ok(batches);
    }

    // ── GET single batch with full entries ───────────────────────────────────
    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Entries)
            .ThenInclude(e => e.Employee)
            .FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == businessId.Value);

        if (batch is null) return NotFound();
        return Ok(batch);
    }

    // ── CREATE a new pay period (PAY-2) ───────────────────────────────────────
    [HttpPost]
    public async Task<ActionResult> Create(CreateBatchRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        // Prevent duplicate period (PAY-2 acceptance criteria)
        var duplicate = await dbContext.PayrollBatches.AnyAsync(b =>
            b.BusinessId == businessId.Value &&
            b.PayCycle == request.PayCycle &&
            b.StartDate == request.StartDate.ToUniversalTime() &&
            b.EndDate == request.EndDate.ToUniversalTime());

        if (duplicate)
            return Conflict(new { message = "A payroll batch already exists for this period." });

        var batch = new PayrollBatch
        {
            BusinessId = businessId.Value,
            PayCycle = request.PayCycle,
            StartDate = request.StartDate.ToUniversalTime(),
            EndDate = request.EndDate.ToUniversalTime(),
            Label = string.IsNullOrWhiteSpace(request.Label)
                ? $"{request.PayCycle} – {request.StartDate:MMM yyyy}"
                : request.Label,
            Status = PayrollBatchStatus.Draft
        };

        dbContext.PayrollBatches.Add(batch);
        await dbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = batch.Id }, new { batch.Id, batch.Label, batch.PayCycle, batch.StartDate, batch.EndDate, batch.Status, batch.CreatedAt });
    }

    // ── PROCESS a batch: calculate everyone's pay (PAY-6, PAY-7) ─────────────
    [HttpPost("{id}/process")]
    public async Task<ActionResult> Process(int id, ProcessBatchRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Entries)
            .FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == businessId.Value);

        if (batch is null) return NotFound();
        if (batch.Status == PayrollBatchStatus.Paid)
            return BadRequest(new { message = "Cannot reprocess a batch that has already been paid." });

        // Load tax config (fall back to defaults if not set)
        var taxConfig = await dbContext.TaxConfigurations
            .FirstOrDefaultAsync(t => t.BusinessId == businessId.Value)
            ?? new TaxConfiguration { BusinessId = businessId.Value };

        // Load all active employees for this business
        var employees = await dbContext.Employees
            .Where(e => e.BusinessId == businessId.Value && e.IsActive)
            .ToListAsync();

        // Clear previous entries
        dbContext.PayrollEntries.RemoveRange(batch.Entries);

        var newEntries = new List<PayrollEntry>();
        foreach (var emp in employees)
        {
            var input = request.Entries.FirstOrDefault(e => e.EmployeeId == emp.Id);
            var holiday = input?.HolidayPay ?? 0m;
            var bonus = input?.Bonus ?? 0m;
            var loan = input?.LoanDeduction ?? 0m;

            decimal periodSalary;
            if (string.Equals(emp.EmploymentType, "Hourly", StringComparison.OrdinalIgnoreCase))
            {
                // Hourly: salary = hours worked × hourly rate (direct — no monthly division)
                var hours = input?.Hours ?? 0m;
                periodSalary = hours * emp.GrossSalary;
            }
            else
            {
                // Salary: convert monthly rate to pay-period rate
                periodSalary = batch.PayCycle.ToLower() switch
                {
                    "weekly"      => emp.GrossSalary / 4.333m,
                    "fortnightly" => emp.GrossSalary / 2m,
                    _             => emp.GrossSalary   // Monthly
                };
            }

            var result = payrollService.CalculateWithConfig(periodSalary, holiday, bonus, loan, taxConfig);

            newEntries.Add(new PayrollEntry
            {
                PayrollBatchId = batch.Id,
                EmployeeId = emp.Id,
                BaseSalary = periodSalary,
                HolidayPay = holiday,
                Bonus = bonus,
                GrossPay = result.GrossMonthlySalary,
                EmployeeNis = result.EmployeeNis,
                EmployeeNht = result.EmployeeNht,
                EmployeeEducationTax = result.EmployeeEducationTax,
                EmployeePaye = result.EmployeePaye,
                LoanDeduction = loan,
                EmployerNis = result.EmployerNis,
                EmployerNht = result.EmployerNht,
                EmployerEducationTax = result.EmployerEducationTax,
                EmployerHeart = result.EmployerHeart,
                TotalStatutoryDeductions = result.ConsolidatedPayrollTaxEmployee,
                TotalDeductions = result.ConsolidatedPayrollTaxEmployee + loan,
                NetPay = result.NetMonthlySalary
            });
        }

        dbContext.PayrollEntries.AddRange(newEntries);
        batch.Status = PayrollBatchStatus.Processed;
        await dbContext.SaveChangesAsync();

        // Reload with employee names
        var processedBatch = await dbContext.PayrollBatches
            .Include(b => b.Entries).ThenInclude(e => e.Employee)
            .FirstAsync(b => b.Id == id);

        return Ok(processedBatch);
    }

    // ── MARK as paid ──────────────────────────────────────────────────────────
    [HttpPost("{id}/mark-paid")]
    public async Task<ActionResult> MarkPaid(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var batch = await dbContext.PayrollBatches
            .FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == businessId.Value);

        if (batch is null) return NotFound();
        if (batch.Status != PayrollBatchStatus.Processed)
            return BadRequest(new { message = "Batch must be processed before marking as paid." });

        batch.Status = PayrollBatchStatus.Paid;
        await dbContext.SaveChangesAsync();
        return Ok(new { message = "Batch marked as paid." });
    }

    // ── DELETE ────────────────────────────────────────────────────────────────
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var batch = await dbContext.PayrollBatches
            .FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == businessId.Value);

        if (batch is null) return NotFound();
        if (batch.Status == PayrollBatchStatus.Paid)
            return BadRequest(new { message = "Cannot delete a paid batch." });

        dbContext.PayrollBatches.Remove(batch);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    // ── REMITTANCE REPORT (PAY-9) ─────────────────────────────────────────────
    [HttpGet("{id}/remittance")]
    public async Task<ActionResult> GetRemittance(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var batch = await dbContext.PayrollBatches
            .Include(b => b.Entries)
            .FirstOrDefaultAsync(b => b.Id == id && b.BusinessId == businessId.Value);

        if (batch is null) return NotFound();

        var report = new
        {
            BatchId = batch.Id,
            Label = batch.Label,
            Period = $"{batch.StartDate:dd MMM yyyy} – {batch.EndDate:dd MMM yyyy}",
            EmployeeCount = batch.Entries.Count,
            TotalGross = batch.Entries.Sum(e => e.GrossPay),
            TotalNet = batch.Entries.Sum(e => e.NetPay),
            // Employee-side statutory
            TotalEmployeeNis = batch.Entries.Sum(e => e.EmployeeNis),
            TotalEmployeeNht = batch.Entries.Sum(e => e.EmployeeNht),
            TotalEmployeeEdTax = batch.Entries.Sum(e => e.EmployeeEducationTax),
            TotalEmployeePaye = batch.Entries.Sum(e => e.EmployeePaye),
            TotalLoanDeductions = batch.Entries.Sum(e => e.LoanDeduction),
            // Employer-side
            TotalEmployerNis = batch.Entries.Sum(e => e.EmployerNis),
            TotalEmployerNht = batch.Entries.Sum(e => e.EmployerNht),
            TotalEmployerEdTax = batch.Entries.Sum(e => e.EmployerEducationTax),
            TotalEmployerHeart = batch.Entries.Sum(e => e.EmployerHeart),
            // Grand totals for TAJ remittance
            TotalNisRemittance = batch.Entries.Sum(e => e.EmployeeNis + e.EmployerNis),
            TotalNhtRemittance = batch.Entries.Sum(e => e.EmployeeNht + e.EmployerNht),
            TotalEdTaxRemittance = batch.Entries.Sum(e => e.EmployeeEducationTax + e.EmployerEducationTax),
            TotalPayeRemittance = batch.Entries.Sum(e => e.EmployeePaye),
            TotalHeartRemittance = batch.Entries.Sum(e => e.EmployerHeart),
            GrandTotalRemittance = batch.Entries.Sum(e =>
                e.EmployeeNis + e.EmployerNis +
                e.EmployeeNht + e.EmployerNht +
                e.EmployeeEducationTax + e.EmployerEducationTax +
                e.EmployeePaye + e.EmployerHeart)
        };

        return Ok(report);
    }

    private int? GetBusinessId()
    {
        var claim = User.FindFirstValue("businessId");
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static bool PayCyclesMatch(string? employeePayCycle, string? batchPayCycle)
    {
        return NormalizePayCycle(employeePayCycle) == NormalizePayCycle(batchPayCycle);
    }

    private static string NormalizePayCycle(string? payCycle)
    {
        return (payCycle ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "bi-weekly" => "fortnightly",
            "biweekly" => "fortnightly",
            "fortnightly" => "fortnightly",
            "monthly" => "monthly",
            "weekly" => "weekly",
            var value => value
        };
    }
}
