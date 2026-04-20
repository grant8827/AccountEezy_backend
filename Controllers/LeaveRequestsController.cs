using System.Security.Claims;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public class LeaveRequestDto
{
    public string LeaveType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DaysRequested { get; set; }
    public string? Reason { get; set; }
    public string? DocumentPath { get; set; }
}

public class LeaveApprovalDto
{
    public string Status { get; set; } = string.Empty; // "Approved" or "Rejected"
    public string? AdminNotes { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class LeaveRequestsController(AppDbContext dbContext, IWebHostEnvironment env) : ControllerBase
{
    // Get all leave requests (Admin view - for business)
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LeaveRequest>>> GetAll()
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var leaveRequests = await dbContext.LeaveRequests
            .Include(lr => lr.Employee)
            .Where(lr => lr.Employee!.BusinessId == businessId.Value)
            .OrderByDescending(lr => lr.RequestedOn)
            .Select(lr => new
            {
                lr.Id,
                lr.EmployeeId,
                Employee = lr.Employee == null ? null : new { lr.Employee.Id, lr.Employee.Name, lr.Employee.Email },
                lr.LeaveType,
                lr.StartDate,
                lr.EndDate,
                lr.DaysRequested,
                lr.Reason,
                lr.Status,
                lr.AdminNotes,
                lr.RequestedOn,
                lr.ReviewedOn,
                lr.DocumentPath
            })
            .ToListAsync();

        return Ok(leaveRequests);
    }

    // Get leave requests for a specific employee (Admin or Employee view)
    [Authorize]
    [HttpGet("employee/{employeeId}")]
    public async Task<ActionResult<IEnumerable<LeaveRequest>>> GetByEmployee(int employeeId)
    {
        var businessId = GetBusinessId();
        var employeeIdClaim = User.FindFirstValue("employeeId");

        // If admin, verify the requested employee belongs to their business
        if (businessId is not null)
        {
            var employee = await dbContext.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.BusinessId == businessId.Value);
            if (employee is null) return NotFound();
        }
        else if (int.TryParse(employeeIdClaim, out var callerEmployeeId))
        {
            // Employee can only read their own leave history
            if (callerEmployeeId != employeeId) return Forbid();
        }
        else
        {
            return Unauthorized();
        }

        var leaveRequests = await dbContext.LeaveRequests
            .Where(lr => lr.EmployeeId == employeeId)
            .OrderByDescending(lr => lr.RequestedOn)
            .ToListAsync();

        return Ok(leaveRequests);
    }

    // Create leave request (Employee creates their own request)
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<LeaveRequest>> Create(LeaveRequestDto request)
    {
        // Get employeeId from claim (set during employee login)
        var employeeIdClaim = User.FindFirstValue("employeeId");
        if (string.IsNullOrEmpty(employeeIdClaim) || !int.TryParse(employeeIdClaim, out var employeeId))
        {
            return Unauthorized("Employee authentication required");
        }

        // Vacation balance check
        if (string.Equals(request.LeaveType, "Vacation", StringComparison.OrdinalIgnoreCase))
        {
            var emp = await dbContext.Employees.FindAsync(employeeId);
            if (emp is null) return NotFound();
            if (emp.VacationDaysBalance < request.DaysRequested)
                return BadRequest(new { error = "You don't have sufficient vacation days." });
        }

        var leaveRequest = new LeaveRequest
        {
            EmployeeId = employeeId,
            LeaveType = request.LeaveType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DaysRequested = request.DaysRequested,
            Reason = request.Reason,
            Status = "Pending",
            DocumentPath = request.DocumentPath,
            RequestedOn = DateTime.UtcNow
        };

        dbContext.LeaveRequests.Add(leaveRequest);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = employeeId }, leaveRequest);
    }

    // Update a pending leave request (employee only)
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, LeaveRequestDto request)
    {
        var employeeIdClaim = User.FindFirstValue("employeeId");
        if (string.IsNullOrEmpty(employeeIdClaim) || !int.TryParse(employeeIdClaim, out var employeeId))
            return Unauthorized(new { error = "Employee authentication required" });

        var leaveRequest = await dbContext.LeaveRequests
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.EmployeeId == employeeId && lr.Status == "Pending");

        if (leaveRequest is null)
            return NotFound(new { error = "Leave request not found or cannot be edited" });

        leaveRequest.LeaveType = request.LeaveType;
        leaveRequest.StartDate = request.StartDate;
        leaveRequest.EndDate = request.EndDate;
        leaveRequest.DaysRequested = request.DaysRequested;
        leaveRequest.Reason = request.Reason;
        if (request.DocumentPath != null)
            leaveRequest.DocumentPath = request.DocumentPath;

        await dbContext.SaveChangesAsync();
        return Ok(leaveRequest);
    }

    // Upload a medical certificate or supporting document
    [Authorize]
    [HttpPost("upload-document")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB max
    public async Task<IActionResult> UploadDocument(IFormFile document)
    {
        if (document == null || document.Length == 0)
            return BadRequest(new { error = "No file provided" });

        if (document.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "File size must not exceed 5 MB" });

        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { error = "Only PDF, JPG, and PNG files are allowed" });

        var uploadsFolder = Path.Combine(env.ContentRootPath, "wwwroot", "uploads", "leaves");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await document.CopyToAsync(stream);
        }

        return Ok(new { documentPath = $"/uploads/leaves/{uniqueFileName}" });
    }

    // Update leave request status (Admin only - approve/reject)
    [Authorize]
    [HttpPut("{id}/status")]
    public async Task<ActionResult<LeaveRequest>> UpdateStatus(int id, LeaveApprovalDto approval)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized("Admin access required");

        var leaveRequest = await dbContext.LeaveRequests
            .Include(lr => lr.Employee)
            .FirstOrDefaultAsync(lr => lr.Id == id && lr.Employee!.BusinessId == businessId.Value);

        if (leaveRequest is null) return NotFound();

        var previousStatus = leaveRequest.Status;
        leaveRequest.Status = approval.Status;
        leaveRequest.AdminNotes = approval.AdminNotes;
        leaveRequest.ReviewedOn = DateTime.UtcNow;

        // Deduct vacation days when approving a vacation leave
        if (string.Equals(approval.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(previousStatus, "Approved", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(leaveRequest.LeaveType, "Vacation", StringComparison.OrdinalIgnoreCase) &&
            leaveRequest.Employee is not null)
        {
            leaveRequest.Employee.VacationDaysBalance = Math.Max(
                0, leaveRequest.Employee.VacationDaysBalance - leaveRequest.DaysRequested);
        }

        await dbContext.SaveChangesAsync();

        return Ok(leaveRequest);
    }

    // Delete leave request
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var employeeIdClaim = User.FindFirstValue("employeeId");
        var businessId = GetBusinessId();

        var leaveRequest = await dbContext.LeaveRequests
            .Include(lr => lr.Employee)
            .FirstOrDefaultAsync(lr => lr.Id == id);

        if (leaveRequest is null) return NotFound();

        // Allow deletion if: employee owns it OR admin owns the business
        var isEmployee = !string.IsNullOrEmpty(employeeIdClaim) && 
                        int.TryParse(employeeIdClaim, out var empId) && 
                        leaveRequest.EmployeeId == empId;
        var isAdmin = businessId is not null && leaveRequest.Employee!.BusinessId == businessId.Value;

        if (!isEmployee && !isAdmin) return Unauthorized();

        // Restore vacation days if deleting an approved vacation request
        if (string.Equals(leaveRequest.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(leaveRequest.LeaveType, "Vacation", StringComparison.OrdinalIgnoreCase) &&
            leaveRequest.Employee is not null)
        {
            leaveRequest.Employee.VacationDaysBalance += leaveRequest.DaysRequested;
        }

        dbContext.LeaveRequests.Remove(leaveRequest);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private int? GetBusinessId()
    {
        var claim = User.FindFirstValue("businessId") ?? User.FindFirstValue(ClaimTypes.GroupSid);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
