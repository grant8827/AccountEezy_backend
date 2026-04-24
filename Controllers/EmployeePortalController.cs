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

        var rawEntries = await dbContext.PayrollEntries
            .Include(e => e.Batch)
            .Include(e => e.Employee)
                .ThenInclude(emp => emp!.Business)
            .Where(e =>
                e.EmployeeId == employeeId.Value &&
                e.Batch != null &&
                e.Batch.Status != 0) // exclude Draft batches
            .OrderByDescending(e => e.Batch!.StartDate)
            .ToListAsync();

        var entries = rawEntries.Select(e => new
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
            netPay          = e.NetPay,
            ytdGross           = e.Employee?.YtdGross ?? 0m,
            ytdNis             = e.Employee?.YtdNis ?? 0m,
            ytdNht             = e.Employee?.YtdNht ?? 0m,
            ytdEdTax           = e.Employee?.YtdEducationTax ?? 0m,
            ytdPaye            = e.Employee?.YtdPaye ?? 0m,
            ytdTotalDeductions = e.Employee?.YtdTotalDeductions ?? 0m,
            position           = e.Employee?.Position,
            businessName       = e.Employee?.Business?.CompanyName,
            businessAddress    = string.Join(", ", new[] { e.Employee?.Business?.Street, e.Employee?.Business?.City, e.Employee?.Business?.Parish }.Where(s => !string.IsNullOrWhiteSpace(s))),
            businessEmail      = e.Employee?.Business?.BusinessEmail,
            businessPhone      = e.Employee?.Business?.BusinessPhone,
            businessLogoUrl    = e.Employee?.Business?.LogoUrl != null ? $"{Request.Scheme}://{Request.Host}{e.Employee.Business.LogoUrl}" : null
        }).ToList();

        return Ok(entries);
    }

    // ── GET /api/employee-portal/leaves ──────────────────────────────────────
    /// Returns this employee's own leave requests plus their vacation days balance.
    [HttpGet("leaves")]
    public async Task<ActionResult> GetLeaves()
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null) return Unauthorized();

        var vacationDaysBalance = await dbContext.Employees
            .Where(e => e.Id == employeeId.Value)
            .Select(e => (decimal?)e.VacationDaysBalance)
            .FirstOrDefaultAsync() ?? 0m;

        var leaves = await dbContext.LeaveRequests
            .Where(lr => lr.EmployeeId == employeeId.Value)
            .OrderByDescending(lr => lr.RequestedOn)
            .Select(lr => new
            {
                lr.Id,
                lr.LeaveType,
                lr.StartDate,
                lr.EndDate,
                lr.DaysRequested,
                lr.Reason,
                lr.Status,
                lr.AdminNotes,
                lr.RequestedOn
            })
            .ToListAsync();

        return Ok(new { vacationDaysBalance, leaves });
    }

    // ── GET /api/employee-portal/notices ─────────────────────────────────────
    /// Returns all notices posted by the employer for this employee's business.
    [HttpGet("notices")]
    public async Task<ActionResult> GetNotices()
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null) return Unauthorized();

        // Resolve businessId from the employee record
        var businessId = await dbContext.Employees
            .Where(e => e.Id == employeeId.Value)
            .Select(e => (int?)e.BusinessId)
            .FirstOrDefaultAsync();

        if (businessId is null) return Unauthorized();

        var notices = await dbContext.Notices
            .Where(n => n.BusinessId == businessId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.Priority,
                n.Category,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(notices);
    }

    // ── GET /api/employee-portal/profile ─────────────────────────────────────
    /// Returns employee profile info needed for the leave request form.
    [HttpGet("profile")]
    public async Task<ActionResult> GetProfile()
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null) return Unauthorized();

        var profile = await dbContext.Employees
            .Where(e => e.Id == employeeId.Value)
            .Select(e => new
            {
                e.Id,
                name       = e.Name,
                e.Email,
                position   = e.Position,
                department = e.Department
            })
            .FirstOrDefaultAsync();

        if (profile is null) return NotFound();

        return Ok(profile);
    }

    private int? GetEmployeeId()
    {
        // Employee JWT carries "employeeId" claim (set by EmployeeAuthController)
        var claim = User.FindFirstValue("employeeId")
                 ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
