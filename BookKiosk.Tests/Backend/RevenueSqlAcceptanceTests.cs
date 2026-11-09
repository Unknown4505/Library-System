using System.Data;
using System.Text.Json;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Tests.Backend;

public sealed class RevenueSqlAcceptanceTests(DisposableSqlQa db) : IClassFixture<DisposableSqlQa>
{
    [SqlQaFact]
    public async Task Sql_revenue_rejects_masked_invalid_row_and_paid_transition_rechecks_persisted_amount()
    {
        await using var context = db.Context();
        var corrupt = new Order { OrderCode = Guid.NewGuid().ToString("N"), SaleChannel = SaleChannel.Counter,
            PaymentMethod = PaymentMethod.Cash, OrderStatus = OrderStatus.Pending, TotalAmount = -100 };
        context.Orders.Add(corrupt); await context.SaveChangesAsync();
        var orders = new OrderRepository(context);
        Assert.False(await orders.TryTransitionOrderStatusAsync(corrupt.OrderId, OrderStatus.Pending, OrderStatus.Paid, DateTime.UtcNow));
        await context.Orders.Where(x => x.OrderId == corrupt.OrderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.OrderStatus, OrderStatus.Paid).SetProperty(x => x.CompletedAt, new DateTime(2099, 1, 1)));
        context.Orders.Add(new Order { OrderCode = Guid.NewGuid().ToString("N"), SaleChannel = SaleChannel.Counter,
            PaymentMethod = PaymentMethod.Cash, OrderStatus = OrderStatus.Paid, TotalAmount = 1000, CompletedAt = new DateTime(2099, 1, 1) });
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<DataException>(() => new ReportRepository(context).GetRevenueByMonthAsync(2099));
        await context.Orders.Where(x => x.OrderId == corrupt.OrderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalAmount, 100));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(await new ReportRepository(context).GetRevenueByMonthAsync(2099)));
        Assert.Equal(12, json.RootElement.GetArrayLength());
        Assert.Equal(1100, json.RootElement[0].GetProperty("Revenue").GetDecimal());
    }

}
