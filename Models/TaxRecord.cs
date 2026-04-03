using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public enum TaxRecordStatus
{
    Pending = 1,
    Paid = 2
}

public class TaxRecord
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business? Business { get; set; }

    [Range(1, 12)]
    public int Month { get; set; }

    [Range(2020, 2100)]
    public int Year { get; set; }

    public decimal TotalRemittance { get; set; }
    public TaxRecordStatus Status { get; set; }
}
