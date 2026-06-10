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
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
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
