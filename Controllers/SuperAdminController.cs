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
    private static readonly SubscriptionPackage[] DefaultPackages =
    [
        new() { Key = "lite", Name = "Lite", MonthlyPriceJmd = 3500, DisplayOrder = 1 },
        new() { Key = "starter", Name = "Starter", MonthlyPriceJmd = 6500, DisplayOrder = 2 },
        new() { Key = "growth", Name = "Growth", MonthlyPriceJmd = 12500, DisplayOrder = 3 },
        new() { Key = "custom", Name = "Custom", MonthlyPriceJmd = 15000, DisplayOrder = 4, IsCustom = true }
    ];

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

    [HttpGet("packages")]
    public async Task<IActionResult> GetPackages()
    {
        await EnsureDefaultPackages();

        var packages = await dbContext.SubscriptionPackages
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new PackageResponse(
                p.Id,
                p.Key,
                p.Name,
                p.MonthlyPriceJmd,
                p.IsCustom,
                p.DiscountEnabled,
                p.DiscountPercent,
                CalculateDiscountedPrice(p.MonthlyPriceJmd, p.DiscountEnabled, p.DiscountPercent),
                p.UpdatedAt))
            .ToListAsync();

        return Ok(packages);
    }

    [HttpPut("packages/{id}/discount")]
    public async Task<IActionResult> UpdatePackageDiscount(int id, PackageDiscountRequest request)
    {
        var package = await dbContext.SubscriptionPackages.FindAsync(id);
        if (package is null)
        {
            return NotFound(new { message = "Package not found." });
        }

        if (request.DiscountPercent is < 0 or > 100)
        {
            return BadRequest(new { message = "Discount percent must be between 0 and 100." });
        }

        package.DiscountEnabled = request.DiscountEnabled;
        package.DiscountPercent = Math.Round(request.DiscountPercent, 2);
        package.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return Ok(new PackageResponse(
            package.Id,
            package.Key,
            package.Name,
            package.MonthlyPriceJmd,
            package.IsCustom,
            package.DiscountEnabled,
            package.DiscountPercent,
            CalculateDiscountedPrice(package.MonthlyPriceJmd, package.DiscountEnabled, package.DiscountPercent),
            package.UpdatedAt));
    }

    private async Task EnsureDefaultPackages()
    {
        var existingKeys = await dbContext.SubscriptionPackages
            .Select(p => p.Key)
            .ToListAsync();

        var missing = DefaultPackages
            .Where(p => !existingKeys.Contains(p.Key))
            .Select(p => new SubscriptionPackage
            {
                Key = p.Key,
                Name = p.Name,
                MonthlyPriceJmd = p.MonthlyPriceJmd,
                DisplayOrder = p.DisplayOrder,
                IsCustom = p.IsCustom
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.SubscriptionPackages.AddRange(missing);
        await dbContext.SaveChangesAsync();
    }

    private static long CalculateDiscountedPrice(long monthlyPrice, bool enabled, decimal percent)
    {
        if (!enabled || percent <= 0)
        {
            return monthlyPrice;
        }

        return (long)Math.Round(monthlyPrice * (1 - percent / 100m));
    }
}

public sealed record PackageDiscountRequest(bool DiscountEnabled, decimal DiscountPercent);

public sealed record PackageResponse(
    int Id,
    string Key,
    string Name,
    long MonthlyPriceJmd,
    bool IsCustom,
    bool DiscountEnabled,
    decimal DiscountPercent,
    long DiscountedMonthlyPriceJmd,
    DateTime UpdatedAt);
