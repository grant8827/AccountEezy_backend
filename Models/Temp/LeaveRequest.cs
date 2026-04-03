using System;
using System.Collections.Generic;

namespace backend.Models.Temp;

public partial class LeaveRequest
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public string LeaveType { get; set; } = null!;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int DaysRequested { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public string? AdminNotes { get; set; }

    public DateTime RequestedOn { get; set; }

    public DateTime? ReviewedOn { get; set; }

    public int? ReviewedBy { get; set; }

    public string? DocumentPath { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
