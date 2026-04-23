using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace backend.Models;

public class Employee
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business? Business { get; set; }

    [MaxLength(140)]
    public required string Name { get; set; }

    [MaxLength(20)]
    [JsonPropertyName("nisNumber")]
    public required string NISNumber { get; set; }

    public decimal GrossSalary { get; set; }

    [MaxLength(20)]
    public required string PayCycle { get; set; }

    // New fields for employee records
    [MaxLength(20)]
    [JsonPropertyName("trn")]
    public string? TRN { get; set; }

    [MaxLength(50)]
    public string? EmployeeIdNumber { get; set; }

    [MaxLength(100)]
    public string? BankAccountNumber { get; set; }

    [MaxLength(100)]
    public string? BankName { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    // Employee portal login credentials
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(100)]
    public string? Position { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    public DateTime? HireDate { get; set; }

    // Employment type: "Salary" or "Hourly"
    [MaxLength(20)]
    public string EmploymentType { get; set; } = "Salary";

    // Vacation days remaining balance (deducted on approved leave)
    public decimal VacationDaysBalance { get; set; } = 0m;

    // Year-to-date accumulators (updated on each mark-paid)
    public decimal YtdGross { get; set; } = 0m;
    public decimal YtdNis { get; set; } = 0m;
    public decimal YtdNht { get; set; } = 0m;
    public decimal YtdEducationTax { get; set; } = 0m;
    public decimal YtdPaye { get; set; } = 0m;
    public decimal YtdTotalDeductions { get; set; } = 0m;

    // Navigation property for leave requests
    public ICollection<LeaveRequest>? LeaveRequests { get; set; }
}
