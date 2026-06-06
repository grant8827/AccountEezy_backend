using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController(
    AppDbContext dbContext,
    UserManager<AppUser> userManager) : ControllerBase
{
    // ── Stats overview ───────────────────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var total = await dbContext.Businesses.CountAsync();
        var active = await dbContext.Businesses.CountAsync(b => b.Status == "Active");
        var pending = await dbContext.Businesses.CountAsync(b => b.Status == "Pending");
        var suspended = await dbContext.Businesses.CountAsync(b => b.Status == "Suspended");

        return Ok(new { total, active, pending, suspended });
    }

    // ── List all businesses ──────────────────────────────────────────────────
    [HttpGet("businesses")]
    public async Task<IActionResult> GetBusinesses()
    {
        // Query 1: businesses with employee counts (fully SQL-translated)
        var businesses = await dbContext.Businesses
            .OrderByDescending(b => b.TrialStartDate)
            .Select(b => new
            {
                b.Id,
                b.CompanyName,
                b.TRN,
                b.Sector,
                b.BusinessType,
                b.Status,
                b.TrialStartDate,
                b.BusinessEmail,
                b.BusinessPhone,
                b.Parish,
                b.Country,
                b.FirstName,
                b.LastName,
                EmployeeCount = b.Employees.Count
            })
            .ToListAsync();

        // Query 2: all admin emails in one round-trip (no N+1)
        var bizIds = businesses.Select(b => b.Id).ToList();
        var ownerEmails = await dbContext.Users
            .Where(u => u.BusinessId.HasValue && bizIds.Contains(u.BusinessId.Value) && u.IsAdmin)
            .Select(u => new { u.BusinessId, u.Email })
            .ToListAsync();

        var ownerEmailByBusinessId = ownerEmails
            .GroupBy(u => u.BusinessId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(u => u.Email).FirstOrDefault());

        // Join in memory
        var result = businesses.Select(b => new
        {
            b.Id,
            b.CompanyName,
            b.TRN,
            b.Sector,
            b.BusinessType,
            b.Status,
            b.TrialStartDate,
            b.BusinessEmail,
            b.BusinessPhone,
            b.Parish,
            b.Country,
            OwnerName = $"{b.FirstName} {b.LastName}".Trim(),
            b.EmployeeCount,
            OwnerEmail = ownerEmailByBusinessId.GetValueOrDefault(b.Id)
        }).ToList();

        return Ok(result);
    }

    // ── Approve a business ───────────────────────────────────────────────────
    [HttpPost("businesses/{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var business = await dbContext.Businesses.FindAsync(id);
        if (business is null) return NotFound(new { message = "Business not found." });

        business.Status = "Active";
        await dbContext.SaveChangesAsync();

        return Ok(new { message = $"{business.CompanyName} has been approved." });
    }

    // ── Suspend a business ───────────────────────────────────────────────────
    [HttpPost("businesses/{id}/suspend")]
    public async Task<IActionResult> Suspend(int id)
    {
        var business = await dbContext.Businesses.FindAsync(id);
        if (business is null) return NotFound(new { message = "Business not found." });

        business.Status = "Suspended";
        await dbContext.SaveChangesAsync();

        return Ok(new { message = $"{business.CompanyName} has been suspended." });
    }

    // ── Get single business detail ───────────────────────────────────────────
    [HttpGet("businesses/{id}")]
    public async Task<IActionResult> GetBusiness(int id)
    {
        var business = await dbContext.Businesses
            .Include(b => b.Employees)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (business is null) return NotFound(new { message = "Business not found." });

        var owner = await dbContext.Users
            .FirstOrDefaultAsync(u => u.BusinessId == id && u.IsAdmin);

        return Ok(new
        {
            business.Id,
            business.CompanyName,
            business.TRN,
            business.Sector,
            business.BusinessType,
            business.Status,
            business.TrialStartDate,
            business.BusinessEmail,
            business.BusinessPhone,
            business.Parish,
            business.Country,
            business.Street,
            business.City,
            business.PostalCode,
            business.NIS,
            business.RegistrationNumber,
            business.Website,
            OwnerName = (business.FirstName + " " + business.LastName).Trim(),
            OwnerEmail = owner?.Email,
            EmployeeCount = business.Employees.Count
        });
    }
}
