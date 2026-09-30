using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BookKiosk.API.HostedServices;

public class ExpiredOrderCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpiredOrderCleanupService> _logger;

    public ExpiredOrderCleanupService(IServiceProvider serviceProvider, ILogger<ExpiredOrderCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredOrderCleanupService is starting.");

        // Chạy định kỳ mỗi 1 phút
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing order cleanup.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task CleanupExpiredOrdersAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Lấy thời điểm cách đây 4 phút
        var timeoutThreshold = DateTime.Now.AddMinutes(-4);

        // Tìm các đơn hàng Pending quá 4 phút
        var expiredOrders = await context.Orders
            .Include(o => o.OrderDetails)
            .Where(o => o.OrderStatus == OrderStatus.Pending && o.CreatedAt < timeoutThreshold)
            .ToListAsync(stoppingToken);

        if (!expiredOrders.Any()) return;

        using var transaction = await context.Database.BeginTransactionAsync(stoppingToken);
        try
        {
            foreach (var order in expiredOrders)
            {
                order.OrderStatus = OrderStatus.Cancelled;
                _logger.LogInformation($"Cancelling expired order: {order.OrderCode}");

                foreach (var detail in order.OrderDetails)
                {
                    // Nhả tồn kho tạm giữ (Trừ đi số lượng trong ReservedQuantity)
                    await context.Books
                        .Where(b => b.BookId == detail.BookId)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - detail.Quantity), stoppingToken);
                }
            }

            await context.SaveChangesAsync(stoppingToken);
            await transaction.CommitAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(stoppingToken);
            _logger.LogError(ex, "Rollback transaction in CleanupExpiredOrdersAsync due to error.");
        }
    }
}
