using System.Security.Claims;
using backend.Data;
using backend.DTOs.Transactions;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TransactionsController(AppDbContext dbContext) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAll([FromQuery] int? frequency = null)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var query = dbContext.Transactions
            .Where(t => t.BusinessId == businessId.Value);

        if (frequency.HasValue)
            query = query.Where(t => (int)t.Frequency == frequency.Value);

        var items = await query
            .OrderByDescending(t => t.Date)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type,
                GctApplicable = t.GctApplicable,
                GctAmount = t.GctAmount,
                Category = t.Category,
                Description = t.Description,
                Frequency = t.Frequency,
                Status = t.Status,
                Date = t.Date
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create(TransactionRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var entry = new TransactionEntry
        {
            BusinessId = businessId.Value,
            Amount = request.Amount,
            Type = request.Type,
            GctApplicable = request.GctApplicable,
            GctAmount = PayrollService.CalculateGct(request.Amount, request.GctApplicable),
            Category = request.Category,
            Description = request.Description,
            Frequency = request.Frequency,
            Status = request.Status,
            Date = request.Date
        };

        dbContext.Transactions.Add(entry);
        await dbContext.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, new TransactionResponse
        {
            Id = entry.Id,
            Amount = entry.Amount,
            Type = entry.Type,
            GctApplicable = entry.GctApplicable,
            GctAmount = entry.GctAmount,
            Category = entry.Category,
            Description = entry.Description,
            Frequency = entry.Frequency,
            Status = entry.Status,
            Date = entry.Date
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TransactionResponse>> GetById(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var item = await dbContext.Transactions
            .Where(t => t.BusinessId == businessId.Value && t.Id == id)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                Amount = t.Amount,
                Type = t.Type,
                GctApplicable = t.GctApplicable,
                GctAmount = t.GctAmount,
                Category = t.Category,
                Description = t.Description,
                Frequency = t.Frequency,
                Status = t.Status,
                Date = t.Date
            })
            .FirstOrDefaultAsync();

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TransactionResponse>> Update(int id, TransactionRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var entry = await dbContext.Transactions
            .FirstOrDefaultAsync(t => t.BusinessId == businessId.Value && t.Id == id);

        if (entry is null) return NotFound();

        entry.Amount = request.Amount;
        entry.Type = request.Type;
        entry.GctApplicable = request.GctApplicable;
        entry.GctAmount = PayrollService.CalculateGct(request.Amount, request.GctApplicable);
        entry.Category = request.Category;
        entry.Description = request.Description;
        entry.Frequency = request.Frequency;
        entry.Status = request.Status;
        entry.Date = request.Date;

        await dbContext.SaveChangesAsync();

        return Ok(new TransactionResponse
        {
            Id = entry.Id,
            Amount = entry.Amount,
            Type = entry.Type,
            GctApplicable = entry.GctApplicable,
            GctAmount = entry.GctAmount,
            Category = entry.Category,
            Description = entry.Description,
            Frequency = entry.Frequency,
            Status = entry.Status,
            Date = entry.Date
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var entry = await dbContext.Transactions
            .FirstOrDefaultAsync(t => t.BusinessId == businessId.Value && t.Id == id);

        if (entry is null) return NotFound();

        dbContext.Transactions.Remove(entry);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
