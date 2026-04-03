using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class TaxConfiguration
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public Business? Business { get; set; }

    // Employee rates
    public decimal NisRateEmployee { get; set; } = 0.03m;
    public decimal NhtRateEmployee { get; set; } = 0.02m;
    public decimal EducationTaxRateEmployee { get; set; } = 0.0225m;
    public decimal PayeRateLower { get; set; } = 0.25m;
    public decimal PayeRateUpper { get; set; } = 0.30m;

    // Employer rates
    public decimal NisRateEmployer { get; set; } = 0.03m;
    public decimal NhtRateEmployer { get; set; } = 0.03m;
    public decimal EducationTaxRateEmployer { get; set; } = 0.035m;
    public decimal HeartRateEmployer { get; set; } = 0.03m;

    // Thresholds & ceilings
    public decimal IncomeTaxThresholdAnnual { get; set; } = 1_902_360m;
    public decimal PayeUpperBandAnnual { get; set; } = 6_000_000m;
    public decimal NisAnnualCeiling { get; set; } = 6_000_000m;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
