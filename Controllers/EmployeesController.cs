using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace backend.Controllers;

public class EmployeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string NisNumber { get; set; } = string.Empty;
    public decimal GrossSalary { get; set; } // Monthly gross salary
    public JsonElement? PayCycle { get; set; }

    // New fields for employee records
    public string? TRN { get; set; }
    public string? EmployeeIdNumber { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }

    // Employment type and vacation balance
    public string EmploymentType { get; set; } = "Salary";
    public decimal VacationDaysBalance { get; set; } = 0m;

    // Job type: "Full-Time", "Part-Time", or "Contract"
    public string JobType { get; set; } = "Full-Time";

    // Position, department, hire date
    public string? Position { get; set; }
    public string? Department { get; set; }
    public DateTime? HireDate { get; set; }

    // Status
    public bool IsActive { get; set; } = true;

    // Employee portal credentials
    public string? Email { get; set; }
    public string? Password { get; set; }

    // YTD opening balances (editable on both create and edit)
    public decimal YtdGross { get; set; } = 0m;
    public decimal YtdNis { get; set; } = 0m;
    public decimal YtdNht { get; set; } = 0m;
    public decimal YtdEducationTax { get; set; } = 0m;
    public decimal YtdPaye { get; set; } = 0m;
    public decimal YtdTotalDeductions { get; set; } = 0m;

    public PayCycle ResolvePayCycle()
    {
        if (PayCycle is null)
        {
            return Models.PayCycle.Monthly;
        }

        var value = PayCycle.Value;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var numericValue) &&
                                      Enum.IsDefined(typeof(PayCycle), numericValue)
                => (PayCycle)numericValue,
            JsonValueKind.String when Enum.TryParse<PayCycle>(
                value.GetString()?.Replace("Bi-Weekly", "Fortnightly"),
                ignoreCase: true,
                out var parsedValue)
                => parsedValue,
            _ => Models.PayCycle.Monthly
        };
    }
}

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EmployeesController(AppDbContext dbContext, UserManager<AppUser> userManager) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Employee>>> GetAll()
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var employees = await dbContext.Employees
            .Where(e => e.BusinessId == businessId.Value)
            .OrderBy(e => e.Name)
            .Select(e => new
            {
                e.Id,
                e.BusinessId,
                e.Name,
                nisNumber = e.NISNumber,
                e.GrossSalary,
                e.PayCycle,
                trn = e.TRN,
                e.EmployeeIdNumber,
                e.BankAccountNumber,
                e.BankName,
                e.DateOfBirth,
                e.PhoneNumber,
                e.Address,
                e.Email,
                e.IsActive,
                e.EmploymentType,
                e.JobType,
                e.VacationDaysBalance,
                e.Position,
                e.Department,
                e.HireDate,
                e.YtdGross,
                e.YtdNis,
                e.YtdNht,
                e.YtdEducationTax,
                e.YtdPaye,
                e.YtdTotalDeductions
            })
            .ToListAsync();

        return Ok(employees);
    }

    [HttpPost]
    public async Task<ActionResult<Employee>> Create(EmployeeRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        // Create employee record
        var employee = new Employee
        {
            BusinessId = businessId.Value,
            Name = request.Name,
            NISNumber = request.NisNumber,
            GrossSalary = request.GrossSalary,
            PayCycle = request.ResolvePayCycle(),
            TRN = request.TRN,
            EmployeeIdNumber = request.EmployeeIdNumber,
            BankAccountNumber = request.BankAccountNumber,
            BankName = request.BankName,
            DateOfBirth = request.DateOfBirth.HasValue
                ? DateTime.SpecifyKind(request.DateOfBirth.Value, DateTimeKind.Utc)
                : null,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            Email = request.Email,
            PasswordHash = !string.IsNullOrEmpty(request.Password)
                ? BCrypt.Net.BCrypt.HashPassword(request.Password)
                : null,
            EmploymentType = request.EmploymentType,
            JobType = request.JobType,
            VacationDaysBalance = request.VacationDaysBalance,
            Position = request.Position,
            Department = request.Department,
            HireDate = request.HireDate.HasValue
                ? DateTime.SpecifyKind(request.HireDate.Value, DateTimeKind.Utc)
                : null,
            YtdGross = request.YtdGross,
            YtdNis = request.YtdNis,
            YtdNht = request.YtdNht,
            YtdEducationTax = request.YtdEducationTax,
            YtdPaye = request.YtdPaye,
            YtdTotalDeductions = request.YtdTotalDeductions
        };

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();

        // Create user account in AspNetUsers if email and password provided
        if (!string.IsNullOrEmpty(request.Email) && !string.IsNullOrEmpty(request.Password))
        {
            var existingUser = await userManager.FindByEmailAsync(request.Email);
            if (existingUser == null)
            {
                var appUser = new AppUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    EmailConfirmed = true,
                    BusinessId = businessId.Value
                };

                await userManager.CreateAsync(appUser, request.Password);
            }
        }

        return CreatedAtAction(nameof(GetAll), new { id = employee.Id }, new
        {
            employee.Id,
            employee.BusinessId,
            employee.Name,
            nisNumber = employee.NISNumber,
            grossSalary = employee.GrossSalary,
            payCycle = employee.PayCycle,
            trn = employee.TRN,
            employeeIdNumber = employee.EmployeeIdNumber,
            bankAccountNumber = employee.BankAccountNumber,
            bankName = employee.BankName,
            dateOfBirth = employee.DateOfBirth,
            phoneNumber = employee.PhoneNumber,
            address = employee.Address,
            email = employee.Email,
            isActive = employee.IsActive,
            employmentType = employee.EmploymentType,
            jobType = employee.JobType,
            vacationDaysBalance = employee.VacationDaysBalance,
            position = employee.Position,
            department = employee.Department,
            hireDate = employee.HireDate,
            ytdGross = employee.YtdGross,
            ytdNis = employee.YtdNis,
            ytdNht = employee.YtdNht,
            ytdEducationTax = employee.YtdEducationTax,
            ytdPaye = employee.YtdPaye,
            ytdTotalDeductions = employee.YtdTotalDeductions
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Employee>> Update(int id, EmployeeRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.BusinessId == businessId.Value);

        if (employee is null) return NotFound();

        var oldEmail = employee.Email;

        employee.Name = request.Name;
        employee.NISNumber = request.NisNumber;
        employee.GrossSalary = request.GrossSalary;
        employee.PayCycle = request.ResolvePayCycle();
        employee.TRN = request.TRN;
        employee.EmployeeIdNumber = request.EmployeeIdNumber;
        employee.BankAccountNumber = request.BankAccountNumber;
        employee.BankName = request.BankName;
        employee.DateOfBirth = request.DateOfBirth.HasValue
            ? DateTime.SpecifyKind(request.DateOfBirth.Value, DateTimeKind.Utc)
            : null;
        employee.PhoneNumber = request.PhoneNumber;
        employee.Address = request.Address;
        employee.Email = request.Email;
        employee.IsActive = request.IsActive;
        employee.EmploymentType = request.EmploymentType;
        employee.JobType = request.JobType;
        employee.VacationDaysBalance = request.VacationDaysBalance;
        employee.Position = request.Position;
        employee.Department = request.Department;
        employee.HireDate = request.HireDate.HasValue
            ? DateTime.SpecifyKind(request.HireDate.Value, DateTimeKind.Utc)
            : null;
        employee.YtdGross = request.YtdGross;
        employee.YtdNis = request.YtdNis;
        employee.YtdNht = request.YtdNht;
        employee.YtdEducationTax = request.YtdEducationTax;
        employee.YtdPaye = request.YtdPaye;
        employee.YtdTotalDeductions = request.YtdTotalDeductions;

        // Only update password if a new one is provided
        if (!string.IsNullOrEmpty(request.Password))
        {
            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        await dbContext.SaveChangesAsync();

        // Update user account if email or password changed
        if (!string.IsNullOrEmpty(oldEmail))
        {
            var appUser = await userManager.FindByEmailAsync(oldEmail);
            if (appUser != null)
            {
                // Update email if changed
                if (!string.IsNullOrEmpty(request.Email) && request.Email != oldEmail)
                {
                    appUser.Email = request.Email;
                    appUser.UserName = request.Email;
                    appUser.NormalizedEmail = request.Email.ToUpper();
                    appUser.NormalizedUserName = request.Email.ToUpper();
                    await userManager.UpdateAsync(appUser);
                }

                // Update password if provided
                if (!string.IsNullOrEmpty(request.Password))
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(appUser);
                    await userManager.ResetPasswordAsync(appUser, token, request.Password);
                }
            }
            else if (!string.IsNullOrEmpty(request.Email) && !string.IsNullOrEmpty(request.Password))
            {
                // Create user account if it doesn't exist but email/password provided
                var newUser = new AppUser
                {
                    UserName = request.Email,
                    Email = request.Email,
                    EmailConfirmed = true,
                    BusinessId = businessId.Value
                };
                await userManager.CreateAsync(newUser, request.Password);
            }
        }

        return Ok(employee);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(e => e.Id == id && e.BusinessId == businessId.Value);

        if (employee is null) return NotFound();

        // Delete user account from AspNetUsers if exists
        if (!string.IsNullOrEmpty(employee.Email))
        {
            var appUser = await userManager.FindByEmailAsync(employee.Email);
            if (appUser != null)
                await userManager.DeleteAsync(appUser);
        }

        dbContext.Employees.Remove(employee);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
