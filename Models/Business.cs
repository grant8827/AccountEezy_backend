using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Business
{
    public int Id { get; set; }

    [MaxLength(160)]
    public required string CompanyName { get; set; }

    [MaxLength(20)]
    public required string TRN { get; set; }

    [MaxLength(80)]
    public required string Sector { get; set; }

    public DateTime TrialStartDate { get; set; } = DateTime.UtcNow;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<TransactionEntry> Transactions { get; set; } = new List<TransactionEntry>();
    public ICollection<TaxRecord> TaxRecords { get; set; } = new List<TaxRecord>();
}
