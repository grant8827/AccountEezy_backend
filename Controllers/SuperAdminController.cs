using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "SuperAdmin")]
public class SuperAdminController(AppDbContext dbContext) : BaseApiController
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
        var active = await dbContext.Businesses.CountAsync(b => b.Status == BusinessStatus.Active);
        var pending = await dbContext.Businesses.CountAsync(b => b.Status == BusinessStatus.Pending);
        var suspended = await dbContext.Businesses.CountAsync(b => b.Status == BusinessStatus.Suspended);
        var deactivated = await dbContext.Businesses.CountAsync(b => b.Status == BusinessStatus.Deactivated);

        return Ok(new { total, active, pending, suspended, deactivated });
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
                b.PaymentStatus,
                b.SubscriptionStatus,
                b.SelectedPlan,
                b.BillingPeriod,
                b.PaymentCompletedAt,
                b.NextPaymentDueAt,
                b.GracePeriodEndsAt,
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
            b.PaymentStatus,
            b.SubscriptionStatus,
            b.SelectedPlan,
            b.BillingPeriod,
            b.PaymentCompletedAt,
            b.NextPaymentDueAt,
            b.GracePeriodEndsAt,
            b.TrialStartDate,
            b.BusinessEmail,
            b.BusinessPhone,
            b.Parish,
            b.Country,
            OwnerName = (b.FirstName + " " + b.LastName).Trim(),
            OwnerEmail = ownerEmailByBusinessId.GetValueOrDefault(b.Id),
            b.EmployeeCount
        });

        return Ok(result);
    }

    // ── Approve a business ───────────────────────────────────────────────────
    [HttpPost("businesses/{id}/approve")]
    public async Task<IActionResult> Approve(int id)
    {
        return await SetBusinessStatus(id, BusinessStatus.Active, "activated");
    }

    // ── Activate a business ─────────────────────────────────────────────────
    [HttpPost("businesses/{id}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        return await SetBusinessStatus(id, BusinessStatus.Active, "activated");
    }

    // ── Update Business Subscription ─────────────────────────────────────────
    [HttpPut("businesses/{id}/subscription")]
    public async Task<IActionResult> UpdateBusinessSubscription(int id, [FromBody] UpdateSubscriptionRequest request)
    {
        var business = await dbContext.Businesses.FindAsync(id);
        if (business is null) return NotFound(new { message = "Business not found." });

        if (!string.IsNullOrEmpty(request.BillingPeriod))
        {
            if (!IsValidBillingPeriod(request.BillingPeriod))
            {
                return BadRequest(new { message = "Billing period must be 'Monthly' or 'Yearly'." });
            }
            business.BillingPeriod = NormalizeBillingPeriod(request.BillingPeriod);
        }

        if (!string.IsNullOrWhiteSpace(request.SelectedPlan))
        {
            business.SelectedPlan = request.SelectedPlan.Trim().ToLowerInvariant();
        }

        if (request.PaymentStatus == PaymentStatus.Paid)
        {
            var paidAt = DateTime.UtcNow;
            business.PaymentStatus = PaymentStatus.Paid;
            business.Status = BusinessStatus.Active;
            business.SubscriptionStatus = SubscriptionStatus.Active;
            business.PaymentCompletedAt = paidAt;
            business.LastPaymentMethod = "Offline";
            
            // Default to monthly if not set
            if (string.IsNullOrEmpty(business.BillingPeriod))
            {
                business.BillingPeriod = "Monthly";
            }

            ApplyPaymentWindow(business, paidAt);
        }
        else if (request.PaymentStatus == PaymentStatus.Unpaid)
        {
            business.PaymentStatus = PaymentStatus.Unpaid;
            business.Status = BusinessStatus.Suspended;
            business.SubscriptionStatus = SubscriptionStatus.Unpaid;
            business.GracePeriodEndsAt ??= DateTime.UtcNow;
        }

        if (request.Status.HasValue)
        {
            business.Status = request.Status.Value;
        }

        await dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Business subscription updated successfully.",
            business.Id,
            business.Status,
            business.PaymentStatus,
            business.SubscriptionStatus,
            business.SelectedPlan,
            business.BillingPeriod,
            business.PaymentCompletedAt,
            business.SubscriptionStartedAt,
            business.NextPaymentDueAt,
            business.GracePeriodEndsAt,
            business.LastPaymentMethod
        });
    }

    // ── Suspend a business ───────────────────────────────────────────────────
    [HttpPost("businesses/{id}/suspend")]
    public async Task<IActionResult> Suspend(int id)
    {
        return await SetBusinessStatus(id, BusinessStatus.Suspended, "suspended");
    }

    // ── Deactivate a business ────────────────────────────────────────────────
    [HttpPost("businesses/{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        return await SetBusinessStatus(id, BusinessStatus.Deactivated, "deactivated");
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

    [HttpGet("users/lookup")]
    public async Task<IActionResult> LookupUser([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Email is required." });
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();

        var user = await dbContext.Users
            .Where(u => u.NormalizedEmail == normalizedEmail || u.Email == email.Trim())
            .FirstOrDefaultAsync();

        var business = user?.BusinessId.HasValue == true
            ? await dbContext.Businesses
                .Where(b => b.Id == user.BusinessId.Value)
                .Select(b => new
                {
                    b.Id,
                    b.CompanyName,
                    b.Status,
                    b.TRN,
                    b.BusinessEmail,
                    b.PaymentStatus,
                    b.SubscriptionStatus,
                    b.SelectedPlan,
                    b.BillingPeriod,
                    b.PaymentCompletedAt,
                    b.NextPaymentDueAt,
                    b.GracePeriodEndsAt,
                    b.LastPaymentMethod
                })
                .FirstOrDefaultAsync()
            : null;

        var employee = await dbContext.Employees
            .Where(e => e.Email == email.Trim())
            .Select(e => new
            {
                e.Id,
                e.Name,
                e.Email,
                e.BusinessId,
                e.IsActive
            })
            .FirstOrDefaultAsync();

        if (user is null && employee is null)
        {
            return NotFound(new { message = "No app user or employee found for that email." });
        }

        return Ok(new
        {
            appUser = user is null ? null : new
            {
                user.Id,
                user.Email,
                user.UserName,
                user.BusinessId,
                user.IsAdmin,
                user.IsSuperAdmin,
                user.EmailConfirmed,
                Business = business is null ? null : new
                {
                    business.Id,
                    business.CompanyName,
                    Status = business.Status.ToString(),
                    business.TRN,
                    business.BusinessEmail,
                    PaymentStatus = business.PaymentStatus.ToString(),
                    SubscriptionStatus = business.SubscriptionStatus.ToString(),
                    business.SelectedPlan,
                    business.BillingPeriod,
                    business.PaymentCompletedAt,
                    business.NextPaymentDueAt,
                    business.GracePeriodEndsAt,
                    business.LastPaymentMethod
                }
            },
            employee,
            loginExpectation = GetLoginExpectation(
                business?.Status.ToString(),
                employee?.IsActive,
                user?.IsSuperAdmin ?? false)
        });
    }

    [HttpGet("packages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPackages()
    {
        try
        {
            await EnsureDefaultPackages();

            var packages = await dbContext.SubscriptionPackages
                .OrderBy(p => p.DisplayOrder)
                .Select(p => new PackageResponse(
                    p.Id,
                    p.Key,
                    p.Name,
                    p.MonthlyPriceJmd,
                    p.YearlyPriceJmd,
                    p.IsCustom,
                    p.DiscountEnabled,
                    p.DiscountPercent,
                    p.MonthlySaleEnabled,
                    p.MonthlySalePriceJmd,
                    p.YearlySaleEnabled,
                    p.YearlySalePriceJmd,
                    p.FreeTrialDays,
                    GetEffectiveMonthlyPrice(p),
                    GetRegularYearlyPrice(p),
                    GetEffectiveYearlyPrice(p),
                    p.UpdatedAt))
                .ToListAsync();

            return Ok(packages);
        }
        catch
        {
            // Fallback keeps Super Admin page functional when DB migrations are pending.
            Response.Headers.Append("X-Packages-Fallback", "defaults");
            return Ok(GetDefaultPackages());
        }
    }

    [HttpPut("packages/{id}/discount")]
    public async Task<IActionResult> UpdatePackageDiscount(int id, PackagePricingRequest request)
    {
        if (request.MonthlyPriceJmd <= 0)
        {
            return BadRequest(new { message = "Monthly regular price must be greater than 0." });
        }

        if (request.YearlyPriceJmd is not null && request.YearlyPriceJmd <= 0)
        {
            return BadRequest(new { message = "Yearly regular price must be greater than 0." });
        }

        if (request.MonthlySaleEnabled && (!request.MonthlySalePriceJmd.HasValue || request.MonthlySalePriceJmd <= 0))
        {
            return BadRequest(new { message = "Monthly sale price must be greater than 0 when monthly sale is enabled." });
        }

        if (request.YearlySaleEnabled && (!request.YearlySalePriceJmd.HasValue || request.YearlySalePriceJmd <= 0))
        {
            return BadRequest(new { message = "Yearly sale price must be greater than 0 when yearly sale is enabled." });
        }

        if (request.FreeTrialDays < 0)
        {
            return BadRequest(new { message = "Free trial days cannot be negative." });
        }

        try
        {
            var package = await dbContext.SubscriptionPackages.FindAsync(id);
            if (package is null)
            {
                return NotFound(new { message = "Package not found." });
            }

            package.MonthlyPriceJmd = request.MonthlyPriceJmd;
            package.YearlyPriceJmd = request.YearlyPriceJmd;
            package.DiscountEnabled = false;
            package.DiscountPercent = 0;
            package.MonthlySaleEnabled = request.MonthlySaleEnabled;
            package.MonthlySalePriceJmd = request.MonthlySaleEnabled ? request.MonthlySalePriceJmd : null;
            package.YearlySaleEnabled = request.YearlySaleEnabled;
            package.YearlySalePriceJmd = request.YearlySaleEnabled ? request.YearlySalePriceJmd : null;
            package.FreeTrialDays = request.FreeTrialDays;
            package.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            return Ok(new PackageResponse(
                package.Id,
                package.Key,
                package.Name,
                package.MonthlyPriceJmd,
                package.YearlyPriceJmd,
                package.IsCustom,
                package.DiscountEnabled,
                package.DiscountPercent,
                package.MonthlySaleEnabled,
                package.MonthlySalePriceJmd,
                package.YearlySaleEnabled,
                package.YearlySalePriceJmd,
                package.FreeTrialDays,
                GetEffectiveMonthlyPrice(package),
                GetRegularYearlyPrice(package),
                GetEffectiveYearlyPrice(package),
                package.UpdatedAt));
        }
        catch
        {
            // Fallback keeps Super Admin page usable when DB migrations are pending.
            var fallbackPackage = GetDefaultPackageById(id);
            if (fallbackPackage is null)
            {
                return NotFound(new { message = "Package not found." });
            }

            var now = DateTime.UtcNow;

            Response.Headers.Append("X-Packages-Fallback", "defaults");
            Response.Headers.Append("X-Packages-Note", "Package pricing update not persisted while database schema is unavailable.");

            fallbackPackage.MonthlyPriceJmd = request.MonthlyPriceJmd;
            fallbackPackage.YearlyPriceJmd = request.YearlyPriceJmd;
            fallbackPackage.MonthlySaleEnabled = request.MonthlySaleEnabled;
            fallbackPackage.MonthlySalePriceJmd = request.MonthlySaleEnabled ? request.MonthlySalePriceJmd : null;
            fallbackPackage.YearlySaleEnabled = request.YearlySaleEnabled;
            fallbackPackage.YearlySalePriceJmd = request.YearlySaleEnabled ? request.YearlySalePriceJmd : null;
            fallbackPackage.FreeTrialDays = request.FreeTrialDays;

            return Ok(new PackageResponse(
                fallbackPackage.DisplayOrder,
                fallbackPackage.Key,
                fallbackPackage.Name,
                fallbackPackage.MonthlyPriceJmd,
                fallbackPackage.YearlyPriceJmd,
                fallbackPackage.IsCustom,
                false,
                0,
                fallbackPackage.MonthlySaleEnabled,
                fallbackPackage.MonthlySalePriceJmd,
                fallbackPackage.YearlySaleEnabled,
                fallbackPackage.YearlySalePriceJmd,
                fallbackPackage.FreeTrialDays,
                GetEffectiveMonthlyPrice(fallbackPackage),
                GetRegularYearlyPrice(fallbackPackage),
                GetEffectiveYearlyPrice(fallbackPackage),
                now));
        }
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
                YearlyPriceJmd = p.YearlyPriceJmd,
                DisplayOrder = p.DisplayOrder,
                IsCustom = p.IsCustom,
                FreeTrialDays = p.FreeTrialDays
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

    private static long GetRegularYearlyPrice(SubscriptionPackage package) =>
        package.YearlyPriceJmd ?? (long)Math.Round(package.MonthlyPriceJmd * 12 * 0.8m);

    private static long GetEffectiveMonthlyPrice(SubscriptionPackage package)
    {
        if (package.MonthlySaleEnabled && package.MonthlySalePriceJmd is > 0)
        {
            return package.MonthlySalePriceJmd.Value;
        }

        return CalculateDiscountedPrice(package.MonthlyPriceJmd, package.DiscountEnabled, package.DiscountPercent);
    }

    private static long GetEffectiveYearlyPrice(SubscriptionPackage package)
    {
        if (package.YearlySaleEnabled && package.YearlySalePriceJmd is > 0)
        {
            return package.YearlySalePriceJmd.Value;
        }

        var regularYearlyPrice = GetRegularYearlyPrice(package);
        return package.DiscountEnabled && package.DiscountPercent > 0
            ? (long)Math.Round(regularYearlyPrice * (1 - package.DiscountPercent / 100m))
            : regularYearlyPrice;
    }

    private static bool IsValidBillingPeriod(string billingPeriod)
    {
        return billingPeriod.Equals("Monthly", StringComparison.OrdinalIgnoreCase) ||
            billingPeriod.Equals("Yearly", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBillingPeriod(string billingPeriod)
    {
        return billingPeriod.Equals("Yearly", StringComparison.OrdinalIgnoreCase) ? "Yearly" : "Monthly";
    }

    private static void ApplyPaymentWindow(Business business, DateTime paidAtUtc)
    {
        var billingPeriod = string.IsNullOrWhiteSpace(business.BillingPeriod)
            ? "Monthly"
            : NormalizeBillingPeriod(business.BillingPeriod);

        business.BillingPeriod = billingPeriod;
        business.SubscriptionStartedAt ??= paidAtUtc;
        business.NextPaymentDueAt = billingPeriod == "Yearly"
            ? paidAtUtc.AddYears(1)
            : paidAtUtc.AddDays(30);
        business.GracePeriodEndsAt = business.NextPaymentDueAt.Value.AddDays(7);
    }

    private static List<PackageResponse> GetDefaultPackages()
    {
        var now = DateTime.UtcNow;

        return DefaultPackages
            .OrderBy(p => p.DisplayOrder)
            .Select((p, index) => new PackageResponse(
                index + 1,
                p.Key,
                p.Name,
                p.MonthlyPriceJmd,
                p.YearlyPriceJmd,
                p.IsCustom,
                p.DiscountEnabled,
                p.DiscountPercent,
                p.MonthlySaleEnabled,
                p.MonthlySalePriceJmd,
                p.YearlySaleEnabled,
                p.YearlySalePriceJmd,
                p.FreeTrialDays,
                GetEffectiveMonthlyPrice(p),
                GetRegularYearlyPrice(p),
                GetEffectiveYearlyPrice(p),
                now))
            .ToList();
    }

    private static SubscriptionPackage? GetDefaultPackageById(int id)
    {
        if (id <= 0 || id > DefaultPackages.Length)
        {
            return null;
        }

        return DefaultPackages
            .OrderBy(p => p.DisplayOrder)
            .ElementAtOrDefault(id - 1);
    }

    private static string GetLoginExpectation(string? businessStatus, bool? employeeIsActive, bool isSuperAdmin)
    {
        if (isSuperAdmin)
        {
            return "Super admin should be allowed to log in if the password is valid.";
        }

        return businessStatus switch
        {
            nameof(BusinessStatus.Active) => "Business user should be allowed to log in if the password is valid.",
            nameof(BusinessStatus.Pending) => "Business user is blocked until the business is approved.",
            nameof(BusinessStatus.Suspended) => "Business user is blocked because the business is suspended.",
            nameof(BusinessStatus.Deactivated) => "Business user is blocked because the business is deactivated.",
            null when employeeIsActive == true => "Employee should be allowed to log in if the password is valid.",
            null when employeeIsActive == false => "Employee exists but is inactive.",
            _ => "No active login path is linked to this email."
        };
    }

    private async Task<IActionResult> SetBusinessStatus(int id, BusinessStatus status, string actionLabel)
    {
        var business = await dbContext.Businesses.FindAsync(id);
        if (business is null) return NotFound(new { message = "Business not found." });

        business.Status = status;
        await dbContext.SaveChangesAsync();

        return Ok(new { message = $"{business.CompanyName} has been {actionLabel}.", status = status.ToString() });
    }
}

public sealed record PackagePricingRequest(
    long MonthlyPriceJmd,
    long? YearlyPriceJmd,
    bool MonthlySaleEnabled,
    long? MonthlySalePriceJmd,
    bool YearlySaleEnabled,
    long? YearlySalePriceJmd,
    int FreeTrialDays);

public sealed record UpdateSubscriptionRequest
{
    public PaymentStatus? PaymentStatus { get; init; }
    public string? BillingPeriod { get; init; }
    public string? SelectedPlan { get; init; }
    public BusinessStatus? Status { get; init; }
}

public sealed record PackageResponse(
    int Id,
    string Key,
    string Name,
    long MonthlyPriceJmd,
    long? YearlyPriceJmd,
    bool IsCustom,
    bool DiscountEnabled,
    decimal DiscountPercent,
    bool MonthlySaleEnabled,
    long? MonthlySalePriceJmd,
    bool YearlySaleEnabled,
    long? YearlySalePriceJmd,
    int FreeTrialDays,
    long DiscountedMonthlyPriceJmd,
    long RegularYearlyPriceJmd,
    long DiscountedYearlyPriceJmd,
    DateTime UpdatedAt);
