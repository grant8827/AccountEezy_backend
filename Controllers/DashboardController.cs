using System.Security.Claims;
using backend.Data;
using backend.DTOs.Reports;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController(AppDbContext dbContext, IPayrollService payrollService) : BaseApiController
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> Summary()
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var transactions = await dbContext.Transactions
            .Where(t => t.BusinessId == businessId.Value)
            .ToListAsync();

        var employees = await dbContext.Employees
            .Where(e => e.BusinessId == businessId.Value)
            .ToListAsync();

        var income = transactions
            .Where(t => t.Type == TransactionType.Income)
            .Sum(t => t.Amount);

        var expenses = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .Sum(t => t.Amount);

        var gctLiability = transactions.Sum(t => t.GctAmount);

        var payrollLiability = employees
            .Select(e => payrollService.Calculate(e.GrossSalary).TotalStatutoryRemittance)
            .Sum();

        var netProfit = income - expenses;
        var cashFlow = income - expenses;

        return Ok(new DashboardSummaryResponse
        {
            TotalIncome = Round2(income),
            TotalExpenses = Round2(expenses),
            NetProfit = Round2(netProfit),
            GctLiability = Round2(gctLiability),
            PayrollTaxLiability = Round2(payrollLiability),
            TotalTaxLiability = Round2(gctLiability + payrollLiability),
            CashFlow = Round2(cashFlow)
        });
    }
    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
