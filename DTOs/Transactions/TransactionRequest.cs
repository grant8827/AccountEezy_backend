using backend.Models;

namespace backend.DTOs.Transactions;

public class TransactionRequest
{
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public bool GctApplicable { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TransactionFrequency Frequency { get; set; } = TransactionFrequency.Daily;
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public DateTime Date { get; set; }
}
