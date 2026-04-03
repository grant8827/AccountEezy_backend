using backend.Data;
using backend.DTOs.Auth;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            TrialStartDate = trialStart
        };

        dbContext.Businesses.Add(business);
        await dbContext.SaveChangesAsync();

        // Update ASP.NET Identity user with BusinessId
        user.BusinessId = (int)business.Id;
        await userManager.UpdateAsync(user);

        // Generate JWT token
        var (token, expiresAt) = jwtTokenService.Generate(user);

        var trialExpiresAt = business.TrialStartDate.AddDays(30);
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
        if (user is null)
        {
            return Ok(new AuthResponse
            {
                Success = false,
                Message = "Invalid email or password."
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

        var (token, expiresAt) = jwtTokenService.Generate(user);

        var loginTrialStart = business?.TrialStartDate ?? DateTime.UtcNow;
        var loginTrialExpiresAt = loginTrialStart.AddDays(30);
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
}
