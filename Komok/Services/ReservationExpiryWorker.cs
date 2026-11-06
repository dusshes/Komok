using Komok.Data;
using Komok.Models;
using Microsoft.EntityFrameworkCore;

namespace Komok.Services;

public sealed class ReservationExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<ReservationExpiryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<KomokDbContext>();
                var expiredCount = await db.Products
                    .Where(product => product.InventoryStatus == InventoryStatus.Reserved
                        && product.ReservedUntil <= DateTimeOffset.UtcNow)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(product => product.InventoryStatus, InventoryStatus.Available)
                        .SetProperty(product => product.ReservedUntil, (DateTimeOffset?)null)
                        .SetProperty(product => product.ArrivalAt, (DateTimeOffset?)null)
                        .SetProperty(product => product.BuyerName, (string?)null)
                        .SetProperty(product => product.BuyerMessenger, (string?)null)
                        .SetProperty(product => product.BuyerMessengerContact, (string?)null)
                        .SetProperty(product => product.BuyerPhone, (string?)null)
                        .SetProperty(product => product.BuyerEmail, (string?)null), stoppingToken);
                if (expiredCount > 0)
                    logger.LogInformation("Expired {Count} product reservations.", expiredCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Reservation expiry processing failed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
