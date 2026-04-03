using System;
using System.Collections.Generic;

namespace backend.Models.Temp;

public partial class Employee
{
    public int Id { get; set; }

    public int BusinessId { get; set; }

    public string Name { get; set; } = null!;

    public string Nisnumber { get; set; } = null!;

    public decimal GrossSalary { get; set; }

    public string PayCycle { get; set; } = null!;

    public string? Trn { get; set; }

    public string? EmployeeIdNumber { get; set; }

    public string? BankAccountNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; }

    public string? BankName { get; set; }

    public virtual Business Business { get; set; } = null!;

    public virtual ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
