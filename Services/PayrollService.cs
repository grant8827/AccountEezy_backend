using backend.DTOs.Payroll;
using backend.Models;

namespace backend.Services;

public interface IPayrollService
{
    PayrollResponse Calculate(decimal grossMonthlySalary);
    PayrollResponse CalculateWithConfig(decimal baseSalary, decimal holidayPay, decimal bonus, decimal loanDeduction, TaxConfiguration config, string payCycle = "Monthly");
}

public class PayrollService : IPayrollService
{
    // Default constants (used when no TaxConfiguration exists)
    private const decimal DefaultThresholdAnnual = 1_902_360m;
    private const decimal DefaultNisRateEmployee = 0.03m;
    private const decimal DefaultNisRateEmployer = 0.03m;
    private const decimal DefaultNhtRateEmployee = 0.02m;
    private const decimal DefaultNhtRateEmployer = 0.03m;
    private const decimal DefaultEdTaxRateEmployee = 0.0225m;
    private const decimal DefaultEdTaxRateEmployer = 0.035m;
    private const decimal DefaultHeartRateEmployer = 0.03m;
    private const decimal DefaultNisAnnualCeiling = 5_000_000m;
    private const decimal DefaultPayeUpperBandAnnual = 6_000_000m;

    // Simple calculation using defaults (existing endpoint still works)
    public PayrollResponse Calculate(decimal grossMonthlySalary)
        => CalculateCore(grossMonthlySalary, 0m, 0m, 0m,
            DefaultNisRateEmployee, DefaultNisRateEmployer,
            DefaultNhtRateEmployee, DefaultNhtRateEmployer,
            DefaultEdTaxRateEmployee, DefaultEdTaxRateEmployer,
            DefaultHeartRateEmployer, DefaultThresholdAnnual,
            DefaultPayeUpperBandAnnual, DefaultNisAnnualCeiling,
            0.25m, 0.30m);

    // Full calculation using stored TaxConfiguration
    public PayrollResponse CalculateWithConfig(decimal baseSalary, decimal holidayPay, decimal bonus, decimal loanDeduction, TaxConfiguration cfg, string payCycle = "Monthly")
    {
        var annualPeriods = (payCycle ?? string.Empty).Trim().ToLower() switch
        {
            "weekly"                               => 52,
            "fortnightly" or "bi-weekly" or "biweekly" => 26,
            _                                      => 12
        };
        return CalculateCore(baseSalary, holidayPay, bonus, loanDeduction,
            cfg.NisRateEmployee, cfg.NisRateEmployer,
            cfg.NhtRateEmployee, cfg.NhtRateEmployer,
            cfg.EducationTaxRateEmployee, cfg.EducationTaxRateEmployer,
            cfg.HeartRateEmployer, cfg.IncomeTaxThresholdAnnual,
            cfg.PayeUpperBandAnnual, cfg.NisAnnualCeiling,
            cfg.PayeRateLower, cfg.PayeRateUpper, annualPeriods);
    }

    private static PayrollResponse CalculateCore(
        decimal baseSalary, decimal holidayPay, decimal bonus, decimal loanDeduction,
        decimal nisRateEmp, decimal nisRateEr,
        decimal nhtRateEmp, decimal nhtRateEr,
        decimal edTaxRateEmp, decimal edTaxRateEr,
        decimal heartRateEr, decimal thresholdAnnual, decimal payeUpperAnnual,
        decimal nisAnnualCeiling, decimal payeLower, decimal payeUpper,
        int annualPeriods = 12)
    {
        var grossPeriod = baseSalary + holidayPay + bonus;
        var grossAnnual = grossPeriod * annualPeriods;

        // NIS (capped at annual ceiling)
        var nisApplicablePeriod = Math.Min(grossAnnual, nisAnnualCeiling) / annualPeriods;
        var employeeNis = Round2(nisApplicablePeriod * nisRateEmp);
        var employerNis = Round2(nisApplicablePeriod * nisRateEr);

        // NHT
        var employeeNht = Round2(grossPeriod * nhtRateEmp);
        var employerNht = Round2(grossPeriod * nhtRateEr);

        // Education Tax
        var employeeEdTax = Round2(grossPeriod * edTaxRateEmp);
        var employerEdTax = Round2(grossPeriod * edTaxRateEr);

        // HEART (employer only)
        var employerHeart = Round2(grossPeriod * heartRateEr);

        // PAYE (two bands — annualize correctly then divide back to period)
        var taxableAnnual = Math.Max(0, grossAnnual - thresholdAnnual);
        var lowerBandAnnual = Math.Min(taxableAnnual, Math.Max(0, payeUpperAnnual - thresholdAnnual));
        var upperBandAnnual = Math.Max(0, taxableAnnual - lowerBandAnnual);
        var employeePaye = Round2(((lowerBandAnnual * payeLower) + (upperBandAnnual * payeUpper)) / annualPeriods);

        var statutoryEmployee = Round2(employeeNis + employeeNht + employeeEdTax + employeePaye);
        var totalDeductions = Round2(statutoryEmployee + loanDeduction);
        var netPay = Math.Max(0m, Round2(grossPeriod - totalDeductions));
        var statutoryEmployer = Round2(employerNis + employerNht + employerEdTax + employerHeart);

        return new PayrollResponse
        {
            GrossMonthlySalary = Round2(grossPeriod),
            BaseSalary = Round2(baseSalary),
            HolidayPay = Round2(holidayPay),
            Bonus = Round2(bonus),
            LoanDeduction = Round2(loanDeduction),
            NetMonthlySalary = netPay,
            EmployeeNis = employeeNis,
            EmployeeNht = employeeNht,
            EmployeeEducationTax = employeeEdTax,
            EmployeePaye = employeePaye,
            EmployerNis = employerNis,
            EmployerNht = employerNht,
            EmployerEducationTax = employerEdTax,
            EmployerHeart = employerHeart,
            ConsolidatedPayrollTaxEmployee = statutoryEmployee,
            ConsolidatedPayrollTaxEmployer = statutoryEmployer,
            TotalStatutoryRemittance = Round2(statutoryEmployee + statutoryEmployer)
        };
    }

    public static decimal CalculateGct(decimal amount, bool applicable)
        => applicable ? Round2(amount * 0.15m) : 0m;

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
