using backend.Models;

namespace backend.DTOs.Transactions;

public class TransactionResponse
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public bool GctApplicable { get; set; }
    public decimal GctAmount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TransactionFrequency Frequency { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime Date { get; set; }
}
