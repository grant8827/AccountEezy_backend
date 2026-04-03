using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class LeaveRequest
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    [MaxLength(50)]
    public required string LeaveType { get; set; } // "Vacation", "Sick", "Personal", etc.

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int DaysRequested { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(20)]
    public required string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

    [MaxLength(250)]
    public string? AdminNotes { get; set; }

    public DateTime RequestedOn { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedOn { get; set; }

    public int? ReviewedBy { get; set; } // AppUser Id who approved/rejected

    [MaxLength(500)]
    public string? DocumentPath { get; set; } // Path to uploaded sick leave certificate, etc.
}
