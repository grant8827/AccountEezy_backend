using System.Security.Claims;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public class TaxConfigRequest
{
    public decimal NisRateEmployee { get; set; }
    public decimal NhtRateEmployee { get; set; }
    public decimal EducationTaxRateEmployee { get; set; }
    public decimal PayeRateLower { get; set; }
    public decimal PayeRateUpper { get; set; }
    public decimal NisRateEmployer { get; set; }
    public decimal NhtRateEmployer { get; set; }
    public decimal EducationTaxRateEmployer { get; set; }
    public decimal HeartRateEmployer { get; set; }
    public decimal IncomeTaxThresholdAnnual { get; set; }
    public decimal PayeUpperBandAnnual { get; set; }
    public decimal NisAnnualCeiling { get; set; }
}

[ApiController]
[Authorize]
[Route("api/tax-config")]
public class TaxConfigController(AppDbContext dbContext) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<TaxConfiguration>> Get()
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var config = await dbContext.TaxConfigurations
            .FirstOrDefaultAsync(t => t.BusinessId == businessId.Value);

        if (config is null)
        {
            // Return defaults without saving
            config = new TaxConfiguration { BusinessId = businessId.Value };
        }

        return Ok(config);
    }

    [HttpPut]
    public async Task<ActionResult<TaxConfiguration>> Upsert(TaxConfigRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var config = await dbContext.TaxConfigurations
            .FirstOrDefaultAsync(t => t.BusinessId == businessId.Value);

        if (config is null)
        {
            config = new TaxConfiguration { BusinessId = businessId.Value };
            dbContext.TaxConfigurations.Add(config);
        }

        config.NisRateEmployee = request.NisRateEmployee;
        config.NhtRateEmployee = request.NhtRateEmployee;
        config.EducationTaxRateEmployee = request.EducationTaxRateEmployee;
        config.PayeRateLower = request.PayeRateLower;
        config.PayeRateUpper = request.PayeRateUpper;
        config.NisRateEmployer = request.NisRateEmployer;
        config.NhtRateEmployer = request.NhtRateEmployer;
        config.EducationTaxRateEmployer = request.EducationTaxRateEmployer;
        config.HeartRateEmployer = request.HeartRateEmployer;
        config.IncomeTaxThresholdAnnual = request.IncomeTaxThresholdAnnual;
        config.PayeUpperBandAnnual = request.PayeUpperBandAnnual;
        config.NisAnnualCeiling = request.NisAnnualCeiling;
        config.LastUpdated = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return Ok(config);
    }
}
