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
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public ExpiredOrderCleanupService(
        IServiceProvider serviceProvider, 
        ILogger<ExpiredOrderCleanupService> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
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

        // Đọc số phút timeout từ cấu hình (mặc định 4 phút)
        var timeoutMinutes = _configuration.GetValue<int>("OrderSettings:OrderTimeoutMinutes", 4);
        var timeoutThreshold = DateTime.UtcNow.AddMinutes(-timeoutMinutes);

        // Tìm các đơn hàng Pending quá thời gian quy định (so khớp UTC với CreatedAt)
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
                // Atomic transition từ Pending sang Cancelled
                var affected = await context.Orders
                    .Where(o => o.OrderId == order.OrderId && o.OrderStatus == OrderStatus.Pending)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, OrderStatus.Cancelled), stoppingToken);

                if (affected > 0)
                {
                    _logger.LogInformation("Cancelled expired order: {OrderCode}", order.OrderCode);

                    foreach (var detail in order.OrderDetails)
                    {
                        // Nhả tồn kho tạm giữ (Trừ đi số lượng trong ReservedQuantity)
                        await context.Books
                            .Where(b => b.BookId == detail.BookId)
                            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - detail.Quantity), stoppingToken);
                    }

                    // Hoàn lại điểm thành viên nếu đơn hàng có sử dụng điểm
                    if (order.MemberId.HasValue && order.PointsUsed > 0)
                    {
                        await context.Members
                            .Where(m => m.MemberId == order.MemberId.Value)
                            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Points, m => m.Points + order.PointsUsed), stoppingToken);

                        await context.PointTransactions.AddAsync(new BookKiosk.Domain.Entities.PointTransaction
                        {
                            MemberId = order.MemberId.Value,
                            OrderId = order.OrderId,
                            Points = order.PointsUsed,
                            Type = PointTransactionType.Earned,
                            Description = $"Hoàn {order.PointsUsed} điểm do đơn hàng {order.OrderCode} hết hạn bị hủy"
                        }, stoppingToken);
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
                else
                {
                    _logger.LogInformation("Order {OrderCode} is no longer Pending (likely Paid). Skipping cleanup.", order.OrderCode);
                }
            }

            await transaction.CommitAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(stoppingToken);
            _logger.LogError(ex, "Rollback transaction in CleanupExpiredOrdersAsync due to error.");
        }
    }
}
