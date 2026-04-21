using backend.Data;
using backend.DTOs.Auth;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<AppUser> userManager,
    AppDbContext dbContext,
    IJwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // Check if email already exists
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Ok(new AuthResponse
            {
                Success = false,
                Message = "Email already exists."
            });
        }

        // Check if TRN already registered
        if (await dbContext.Businesses.AnyAsync(b => b.TRN == request.TRN))
        {
            return Ok(new AuthResponse
            {
                Success = false,
                Message = "TRN already registered."
            });
        }

        // Create user in ASP.NET Identity first
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            IsAdmin = true  // Business registrant is always admin
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return Ok(new AuthResponse
            {
                Success = false,
                Message = string.Join(", ", createResult.Errors.Select(e => e.Description))
            });
        }

        // Create business in database
        var trialStart = DateTime.UtcNow;
        var business = new Business
        {
            CompanyName = request.BusinessName,
            TRN = request.TRN,
            Sector = request.Industry ?? request.BusinessType ?? "General",
            TrialStartDate = trialStart,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            RegistrationNumber = request.RegistrationNumber,
            NIS = request.NIS,
            BusinessType = request.BusinessType,
            FiscalYearEnd = request.FiscalYearEnd,
            Street = request.Street,
            City = request.City,
            Parish = request.Parish,
            PostalCode = request.PostalCode,
            Country = request.Country ?? "Jamaica",
            BusinessPhone = request.BusinessPhone,
            BusinessEmail = request.BusinessEmail,
            Website = request.Website
        };

        dbContext.Businesses.Add(business);
        await dbContext.SaveChangesAsync();

        // Update ASP.NET Identity user with BusinessId
        user.BusinessId = (int)business.Id;
        await userManager.UpdateAsync(user);

        // Generate JWT token
        var (token, expiresAt) = jwtTokenService.Generate(user);

        var trialExpiresAt = business.TrialStartDate.AddYears(100); // Trial disabled during development
        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Registration successful",
            Data = new AuthData
            {
                User = new UserData
                {
                    Email = user.Email ?? request.Email,
                    BusinessId = (int)business.Id,
                    BusinessName = business.CompanyName,
                    ExpiresAtUtc = expiresAt,
                    TrialStartDate = business.TrialStartDate,
                    TrialExpiresAt = trialExpiresAt,
                    IsTrialExpired = DateTime.UtcNow > trialExpiresAt,
                    IsAdmin = true,
                    IsEmployee = false
                },
                Token = token
            }
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == request.Email.ToUpper());

        // No AppUser found — try authenticating as an Employee directly
        if (user is null)
        {
            var empUser = await dbContext.Employees
                .FirstOrDefaultAsync(e => e.Email == request.Email && e.IsActive);

            if (empUser is null || string.IsNullOrEmpty(empUser.PasswordHash) ||
                !BCrypt.Net.BCrypt.Verify(request.Password, empUser.PasswordHash))
            {
                return Ok(new AuthResponse { Success = false, Message = "Invalid email or password." });
            }

            var (empToken, empExpires) = jwtTokenService.GenerateForEmployee(empUser);
            return Ok(new AuthResponse
            {
                Success = true,
                Message = "Login successful",
                Data = new AuthData
                {
                    User = new UserData
                    {
                        Email = empUser.Email ?? request.Email,
                        EmployeeId = empUser.Id,
                        EmployeeName = empUser.Name,
                        IsEmployee = true,
                        IsAdmin = false,
                        ExpiresAtUtc = empExpires,
                        TrialStartDate = DateTime.UtcNow,
                        TrialExpiresAt = DateTime.UtcNow.AddYears(100),
                        IsTrialExpired = false
                    },
                    Token = empToken
                }
            });
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return Ok(new AuthResponse
            {
                Success = false,
                Message = "Invalid email or password."
            });
        }

        // Get business details using BusinessId
        var business = user.BusinessId.HasValue 
            ? await dbContext.Businesses
                .FirstOrDefaultAsync(b => b.Id == user.BusinessId.Value)
            : null;

        // Check if this user is an employee (not a business owner)
        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(e => e.Email == request.Email && e.IsActive);

        var (token, expiresAt) = jwtTokenService.Generate(user, employee?.Id);

        var loginTrialStart = business?.TrialStartDate ?? DateTime.UtcNow;
        var loginTrialExpiresAt = loginTrialStart.AddYears(100); // Trial disabled during development
        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Login successful",
            Data = new AuthData
            {
                User = new UserData
                {
                    Email = user.Email ?? request.Email,
                    BusinessId = user.BusinessId,
                    BusinessName = business?.CompanyName,
                    ExpiresAtUtc = expiresAt,
                    TrialStartDate = loginTrialStart,
                    TrialExpiresAt = loginTrialExpiresAt,
                    IsTrialExpired = DateTime.UtcNow > loginTrialExpiresAt,
                    IsEmployee = employee is not null,
                    EmployeeId = employee?.Id,
                    EmployeeName = employee?.Name,
                    IsAdmin = user.IsAdmin
                },
                Token = token
            }
        });
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var appUser = await userManager.FindByIdAsync(userId ?? string.Empty);
        if (appUser is null) return Unauthorized();

        var business = appUser.BusinessId.HasValue
            ? await dbContext.Businesses.FindAsync(appUser.BusinessId.Value)
            : null;

        if (business is null) return NotFound("Business not found.");

        return Ok(new ProfileResponse
        {
            Email = appUser.Email ?? string.Empty,
            FirstName = business.FirstName,
            LastName = business.LastName,
            Phone = business.Phone,
            CompanyName = business.CompanyName,
            TRN = business.TRN,
            Sector = business.Sector,
            RegistrationNumber = business.RegistrationNumber,
            NIS = business.NIS,
            BusinessType = business.BusinessType,
            FiscalYearEnd = business.FiscalYearEnd,
            Street = business.Street,
            City = business.City,
            Parish = business.Parish,
            PostalCode = business.PostalCode,
            Country = business.Country,
            BusinessPhone = business.BusinessPhone,
            BusinessEmail = business.BusinessEmail,
            Website = business.Website
        });
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(UpdateProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var appUser = await userManager.FindByIdAsync(userId ?? string.Empty);
        if (appUser is null) return Unauthorized();

        var business = appUser.BusinessId.HasValue
            ? await dbContext.Businesses.FindAsync(appUser.BusinessId.Value)
            : null;

        if (business is null) return NotFound("Business not found.");

        if (request.CompanyName is not null) business.CompanyName = request.CompanyName;
        if (request.Sector is not null) business.Sector = request.Sector;
        business.FirstName = request.FirstName;
        business.LastName = request.LastName;
        business.Phone = request.Phone;
        business.RegistrationNumber = request.RegistrationNumber;
        business.NIS = request.NIS;
        business.BusinessType = request.BusinessType;
        business.FiscalYearEnd = request.FiscalYearEnd.HasValue
            ? DateTime.SpecifyKind(request.FiscalYearEnd.Value, DateTimeKind.Utc)
            : null;
        business.Street = request.Street;
        business.City = request.City;
        business.Parish = request.Parish;
        business.PostalCode = request.PostalCode;
        if (request.Country is not null) business.Country = request.Country;
        business.BusinessPhone = request.BusinessPhone;
        business.BusinessEmail = request.BusinessEmail;
        business.Website = request.Website;

        await dbContext.SaveChangesAsync();

        return Ok(new ProfileResponse
        {
            Email = appUser.Email ?? string.Empty,
            FirstName = business.FirstName,
            LastName = business.LastName,
            Phone = business.Phone,
            CompanyName = business.CompanyName,
            TRN = business.TRN,
            Sector = business.Sector,
            RegistrationNumber = business.RegistrationNumber,
            NIS = business.NIS,
            BusinessType = business.BusinessType,
            FiscalYearEnd = business.FiscalYearEnd,
            Street = business.Street,
            City = business.City,
            Parish = business.Parish,
            PostalCode = business.PostalCode,
            Country = business.Country,
            BusinessPhone = business.BusinessPhone,
            BusinessEmail = business.BusinessEmail,
            Website = business.Website
        });
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var appUser = await userManager.FindByIdAsync(userId ?? string.Empty);
        if (appUser is null) return Unauthorized();

        var result = await userManager.ChangePasswordAsync(appUser, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Ok(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
        }

        return Ok(new { success = true, message = "Password changed successfully." });
    }
}

