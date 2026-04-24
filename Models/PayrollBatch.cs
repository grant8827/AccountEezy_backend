using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public enum PayrollBatchStatus
{
    Draft = 0,
    Processed = 1,
    Paid = 2
}

public class PayrollBatch
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business? Business { get; set; }

    [MaxLength(20)]
    public required string PayCycle { get; set; } = "Monthly";

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? PayDate { get; set; }

    [MaxLength(30)]
    public string Label { get; set; } = string.Empty;

    public PayrollBatchStatus Status { get; set; } = PayrollBatchStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PayrollEntry> Entries { get; set; } = new List<PayrollEntry>();
}
