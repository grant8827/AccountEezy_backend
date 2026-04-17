using System.Security.Claims;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public class EmployeeRequest
{
    public string Name { get; set; } = string.Empty;
    public string NisNumber { get; set; } = string.Empty;
    public decimal GrossSalary { get; set; }
    public string PayCycle { get; set; } = "Monthly";

    // New fields for employee records
    public string? TRN { get; set; }
    public string? EmployeeIdNumber { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }

    // Employee portal credentials
    public string? Email { get; set; }
    public string? Password { get; set; }
}

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EmployeesController(AppDbContext dbContext, UserManager<AppUser> userManager) : ControllerBase
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
                e.Address,
                e.Email,
                e.IsActive
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
            PayCycle = request.PayCycle,
            TRN = request.TRN,
            EmployeeIdNumber = request.EmployeeIdNumber,
            BankAccountNumber = request.BankAccountNumber,
            BankName = request.BankName,
            DateOfBirth = request.DateOfBirth.HasValue
                ? DateTime.SpecifyKind(request.DateOfBirth.Value, DateTimeKind.Utc)
                : null,
            Address = request.Address,
            Email = request.Email,
            PasswordHash = !string.IsNullOrEmpty(request.Password)
                ? BCrypt.Net.BCrypt.HashPassword(request.Password)
                : null
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
            employee.Id, employee.Name, employee.NISNumber, employee.GrossSalary, employee.PayCycle,
            employee.TRN, employee.EmployeeIdNumber, employee.BankAccountNumber, employee.BankName,
            employee.DateOfBirth, employee.Address, employee.Email, employee.IsActive
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
        employee.PayCycle = request.PayCycle;
        employee.TRN = request.TRN;
        employee.EmployeeIdNumber = request.EmployeeIdNumber;
        employee.BankAccountNumber = request.BankAccountNumber;
        employee.BankName = request.BankName;
        employee.DateOfBirth = request.DateOfBirth.HasValue
            ? DateTime.SpecifyKind(request.DateOfBirth.Value, DateTimeKind.Utc)
            : null;
        employee.Address = request.Address;
        employee.Email = request.Email;

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

    private int? GetBusinessId()
    {
        var claim = User.FindFirstValue("businessId") ?? User.FindFirstValue(ClaimTypes.GroupSid);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
