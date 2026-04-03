using System.Security.Claims;
using backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/employee-portal")]
public class EmployeePortalController(AppDbContext dbContext) : ControllerBase
{
    // ── GET /api/employee-portal/payslips ─────────────────────────────────────
    /// Returns all processed payslip entries for the authenticated employee.
    [HttpGet("payslips")]
    public async Task<ActionResult> GetPayslips()
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null) return Unauthorized();

        var entries = await dbContext.PayrollEntries
            .Include(e => e.Batch)
            .Where(e =>
                e.EmployeeId == employeeId.Value &&
                e.Batch != null &&
                e.Batch.Status != 0) // exclude Draft batches
            .OrderByDescending(e => e.Batch!.StartDate)
            .Select(e => new
            {
                id              = e.Id,
                batchId         = e.PayrollBatchId,
                period          = e.Batch!.Label,
                payCycle        = e.Batch.PayCycle,
                startDate       = e.Batch.StartDate,
                endDate         = e.Batch.EndDate,
                batchStatus     = e.Batch.Status,
                baseSalary      = e.BaseSalary,
                holidayPay      = e.HolidayPay,
                bonus           = e.Bonus,
                grossPay        = e.GrossPay,
                employeeNis     = e.EmployeeNis,
                employeeNht     = e.EmployeeNht,
                employeeEdTax   = e.EmployeeEducationTax,
                employeePaye    = e.EmployeePaye,
                loanDeduction   = e.LoanDeduction,
                employerNis     = e.EmployerNis,
                employerNht     = e.EmployerNht,
                employerEdTax   = e.EmployerEducationTax,
                employerHeart   = e.EmployerHeart,
                deductions      = e.TotalDeductions,
                netPay          = e.NetPay
            })
            .ToListAsync();

        return Ok(entries);
    }

    // ── GET /api/employee-portal/debug ───────────────────────────────────────
    /// Returns all JWT claims so we can verify the token is being read correctly.
    [HttpGet("debug")]
    public ActionResult GetDebug()
    {
        return Ok(new
        {
            resolvedEmployeeId = GetEmployeeId(),
            claims = User.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList()
        });
    }

    private int? GetEmployeeId()
    {
        // Employee JWT carries "employeeId" claim (set by EmployeeAuthController)
        var claim = User.FindFirstValue("employeeId")
                 ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
