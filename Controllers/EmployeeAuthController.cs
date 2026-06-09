using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using backend.Services;

namespace backend.Controllers;

public class EmployeeLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class EmployeeAuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int EmployeeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int BusinessId { get; set; }
}

[ApiController]
[Route("api/employee-auth")]
public class EmployeeAuthController(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions) : BaseApiController
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    [HttpPost("login")]
    public async Task<ActionResult<EmployeeAuthResponse>> Login(EmployeeLoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { message = "Email and password are required" });
        }

        var employee = await dbContext.Employees
            .FirstOrDefaultAsync(e => e.Email == request.Email && e.IsActive);

        if (employee is null || string.IsNullOrEmpty(employee.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // Generate JWT token
        var (token, expiresAtUtc) = GenerateEmployeeToken(employee);

        return Ok(new EmployeeAuthResponse
        {
            Token = token,
            ExpiresAt = expiresAtUtc,
            EmployeeId = employee.Id,
            Name = employee.Name,
            Email = employee.Email ?? string.Empty,
            BusinessId = employee.BusinessId
        });
    }

    private (string token, DateTime expiresAtUtc) GenerateEmployeeToken(Employee employee)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, employee.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, employee.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new(ClaimTypes.Email, employee.Email ?? string.Empty),
            new("employeeId", employee.Id.ToString()),
            new("businessId", employee.BusinessId.ToString()),
            new(ClaimTypes.Role, "Employee") // Add role claim to differentiate from admin
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials
        );

        var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        return (token, expiresAtUtc);
    }
}
