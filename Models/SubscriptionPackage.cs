using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class SubscriptionPackage
{
    public int Id { get; set; }

    [MaxLength(40)]
    public required string Key { get; set; }

    [MaxLength(80)]
    public required string Name { get; set; }

    public long MonthlyPriceJmd { get; set; }

    public long? YearlyPriceJmd { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsCustom { get; set; }

    public bool DiscountEnabled { get; set; }

    public decimal DiscountPercent { get; set; }

    public bool MonthlySaleEnabled { get; set; }

    public long? MonthlySalePriceJmd { get; set; }

    public bool YearlySaleEnabled { get; set; }

    public long? YearlySalePriceJmd { get; set; }

    public int FreeTrialDays { get; set; } = 14;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
