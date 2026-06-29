using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class SubscriptionStatusService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionStatusService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await SuspendPastDueBusinesses(stoppingToken);
            await UpdateEmployeeLeaveStatuses(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task UpdateEmployeeLeaveStatuses(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var today = DateTime.UtcNow.Date;

            // Active employees not yet flagged on-leave but have an approved leave that started and hasn't ended
            var shouldBeOnLeave = await dbContext.Employees
                .Where(e => e.IsActive && !e.IsOnLeave
                    && e.LeaveRequests!.Any(lr =>
                        lr.Status == LeaveRequestStatus.Approved
                        && lr.StartDate.Date <= today
                        && lr.EndDate.Date > today))
                .ToListAsync(cancellationToken);

            foreach (var emp in shouldBeOnLeave)
                emp.IsOnLeave = true;

            // Employees flagged on-leave but no approved leave is currently active
            var shouldBeBack = await dbContext.Employees
                .Where(e => e.IsOnLeave
                    && !e.LeaveRequests!.Any(lr =>
                        lr.Status == LeaveRequestStatus.Approved
                        && lr.StartDate.Date <= today
                        && lr.EndDate.Date > today))
                .ToListAsync(cancellationToken);

            foreach (var emp in shouldBeBack)
                emp.IsOnLeave = false;

            int changed = shouldBeOnLeave.Count + shouldBeBack.Count;
            if (changed > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Updated leave status for {Count} employee(s).", changed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update employee leave statuses.");
        }
    }

    private async Task SuspendPastDueBusinesses(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            var businesses = await dbContext.Businesses
                .Where(b => b.Status == BusinessStatus.Active
                    && b.GracePeriodEndsAt != null
                    && b.GracePeriodEndsAt < now)
                .ToListAsync(cancellationToken);

            foreach (var business in businesses)
            {
                business.Status = BusinessStatus.Suspended;
                business.PaymentStatus = PaymentStatus.Unpaid;
                business.SubscriptionStatus = SubscriptionStatus.PastDue;
            }

            if (businesses.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Suspended {Count} past-due business account(s).", businesses.Count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown requested.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to suspend past-due business accounts.");
        }
    }
}
