using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
[Route("api/notices")]
public class NoticesController(AppDbContext dbContext) : BaseApiController
{
    // ── GET /api/notices ─────────────────────────────────────────────────────
    /// Returns all notices for the authenticated employer's business.
    [HttpGet]
    public async Task<ActionResult> GetNotices()
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var notices = await dbContext.Notices
            .Where(n => n.BusinessId == businessId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.Type,
                n.Priority,
                n.Category,
                n.CreatedAt
            })
            .ToListAsync();

        return Ok(notices);
    }

    // ── POST /api/notices ────────────────────────────────────────────────────
    /// Creates a new notice for the authenticated employer's business.
    [HttpPost]
    public async Task<ActionResult> CreateNotice([FromBody] CreateNoticeRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var notice = new Notice
        {
            BusinessId = businessId.Value,
            Title      = request.Title,
            Message    = request.Message,
            Type       = request.Type,
            Priority   = request.Priority,
            Category   = request.Category,
            CreatedAt  = DateTime.UtcNow
        };

        dbContext.Notices.Add(notice);
        await dbContext.SaveChangesAsync();

        return Ok(new { notice.Id, notice.Title, notice.Message, notice.Type, notice.Priority, notice.Category, notice.CreatedAt });
    }

    // ── PUT /api/notices/{id} ────────────────────────────────────────────────
    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateNotice(int id, [FromBody] CreateNoticeRequest request)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var notice = await dbContext.Notices
            .FirstOrDefaultAsync(n => n.Id == id && n.BusinessId == businessId.Value);

        if (notice is null) return NotFound();

        notice.Title    = request.Title;
        notice.Message  = request.Message;
        notice.Type     = request.Type;
        notice.Priority = request.Priority;
        notice.Category = request.Category;

        await dbContext.SaveChangesAsync();
        return Ok(new { notice.Id, notice.Title, notice.Message, notice.Type, notice.Priority, notice.Category, notice.CreatedAt });
    }

    // ── DELETE /api/notices/{id} ─────────────────────────────────────────────
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteNotice(int id)
    {
        var businessId = GetBusinessId();
        if (businessId is null) return Unauthorized();

        var notice = await dbContext.Notices
            .FirstOrDefaultAsync(n => n.Id == id && n.BusinessId == businessId.Value);

        if (notice is null) return NotFound();

        dbContext.Notices.Remove(notice);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateNoticeRequest(
    string Title,
    string Message,
    string Type,
    string Priority,
    string Category
);
