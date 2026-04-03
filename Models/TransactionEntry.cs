using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public enum TransactionType
{
    Income = 1,
    Expense = 2
}

public enum TransactionFrequency
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

public enum TransactionStatus
{
    Pending = 1,
    Cleared = 2
}

public class TransactionEntry
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business? Business { get; set; }

    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public bool GctApplicable { get; set; }
    public decimal GctAmount { get; set; }

    [MaxLength(80)]
    public required string Category { get; set; }

    [MaxLength(255)]
    public string Description { get; set; } = string.Empty;

    public TransactionFrequency Frequency { get; set; } = TransactionFrequency.Daily;

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public DateTime Date { get; set; }
}
