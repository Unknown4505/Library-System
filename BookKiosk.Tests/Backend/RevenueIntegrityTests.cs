extern alias BackendApi;
using System.Data;
using System.Text.Json;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using BackendApi::BookKiosk.API.Controllers;
using BackendApi::BookKiosk.API.Middleware;

namespace BookKiosk.Tests.Backend;

public class RevenueIntegrityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Corrupt_pending_order_is_rejected_before_any_payment_write(int amount)
    {
        var order = new Order { OrderId = 1, OrderCode = "ORD-TEST", TotalAmount = amount,
            OrderStatus = OrderStatus.Pending, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.Cash };
        var orders = new Mock<IOrderRepository>();
        orders.Setup(x => x.GetOrderByCodeAsync(order.OrderCode)).ReturnsAsync(order);
        orders.Setup(x => x.GetOrderDetailWithRelationsAsync(1)).ReturnsAsync(order);
        var work = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var payments = new Mock<IPaymentRepository>(MockBehavior.Strict);
        var members = new Mock<IMemberRepository>(MockBehavior.Strict);
        var paymentService = new PaymentService(work.Object, payments.Object, orders.Object, members.Object);

        Assert.False(await paymentService.HandleSePayWebhookAsync(new PaymentWebhookPayload
        {
            referenceCode = "ref-test", transferType = "in", accountNumber = "test-account", code = order.OrderCode,
            transferAmount = amount
        }));
        var simulated = await paymentService.SimulateDevSuccessAsync(order.OrderCode);
        Assert.False(simulated.Success);
        Assert.Equal("Conflict", simulated.Status);
        var service = new OrderService(work.Object, orders.Object, Mock.Of<IPromotionRepository>(), members.Object, payments.Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmCounterPaymentAsync(1, 1));
        Assert.Equal(OrderStatus.Pending, order.OrderStatus);
        Assert.Null(order.CompletedAt);
        orders.Verify(x => x.TryTransitionOrderStatusAsync(It.IsAny<int>(), It.IsAny<OrderStatus>(), It.IsAny<OrderStatus>(), It.IsAny<DateTime?>()), Times.Never);
        orders.Verify(x => x.DecreaseStockAndReservedQuantityAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        work.VerifyNoOtherCalls();
        payments.VerifyNoOtherCalls();
        members.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Valid_webhook_and_replay_preserve_single_payment_commit()
    {
        var order = new Order { OrderId = 1, OrderCode = "ORD-TEST", TotalAmount = 50000, OrderStatus = OrderStatus.Pending };
        var orders = new Mock<IOrderRepository>();
        orders.Setup(x => x.GetOrderByCodeAsync(order.OrderCode)).ReturnsAsync(order);
        orders.Setup(x => x.TryTransitionOrderStatusAsync(1, OrderStatus.Pending, OrderStatus.Paid, It.IsAny<DateTime?>())).ReturnsAsync(true);
        var work = new Mock<IUnitOfWork>();
        var payments = new Mock<IPaymentRepository>();
        payments.SetupSequence(x => x.CheckTransactionExistsAsync("ref-test")).ReturnsAsync(false).ReturnsAsync(true);
        var service = new PaymentService(work.Object, payments.Object, orders.Object, Mock.Of<IMemberRepository>());
        var payload = new PaymentWebhookPayload { referenceCode = "ref-test", transferType = "in", accountNumber = "test-account", code = order.OrderCode, transferAmount = 50000 };
        Assert.True(await service.HandleSePayWebhookAsync(payload));
        Assert.True(await service.HandleSePayWebhookAsync(payload));
        Assert.Equal(OrderStatus.Paid, order.OrderStatus);
        payments.Verify(x => x.AddTransactionAsync(It.IsAny<PaymentTransaction>()), Times.Once);
        work.Verify(x => x.CommitAsync(), Times.Once);
        work.Verify(x => x.RollbackAsync(), Times.Once);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    public async Task Invalid_individual_paid_row_is_not_masked_by_positive_month(int invalidAmount)
    {
        await using var context = NewContext();
        context.Orders.AddRange(Paid(1, invalidAmount), Paid(2, 1000));
        await context.SaveChangesAsync();
        var repository = new ReportRepository(context);
        await Assert.ThrowsAsync<DataException>(() => repository.GetRevenueByMonthAsync(2026));

        var response = new DefaultHttpContext();
        response.Response.Body = new MemoryStream();
        var logger = new Mock<ILogger<GlobalExceptionHandlerMiddleware>>();
        var controller = new ReportsController(new ReportService(repository));
        var middleware = new GlobalExceptionHandlerMiddleware(async _ => { await controller.GetRevenueByMonth(2026); }, logger.Object);
        await middleware.InvokeAsync(response);
        Assert.Equal(500, response.Response.StatusCode);
        response.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(response.Response.Body);
        Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("INTERNAL_ERROR", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("data").ValueKind);
        Assert.DoesNotContain("integrity", body.RootElement.GetProperty("message").GetString()!);
        Assert.Contains(logger.Invocations, x => x.Method.Name == "Log" && x.Arguments[0] is LogLevel level && level == LogLevel.Error);
    }

    [Fact]
    public async Task Valid_revenue_retains_paid_only_totals_and_twelve_unique_sorted_months()
    {
        await using var context = NewContext();
        var otherMonth = Paid(3, 700); otherMonth.CompletedAt = new DateTime(2026, 2, 1);
        var pending = Paid(4, -500); pending.OrderStatus = OrderStatus.Pending;
        var cancelled = Paid(5, -500); cancelled.OrderStatus = OrderStatus.Cancelled;
        var otherYear = Paid(6, -500); otherYear.CompletedAt = new DateTime(2025, 1, 1);
        context.Orders.AddRange(Paid(1, 100), Paid(2, 250), otherMonth, pending, cancelled, otherYear);
        await context.SaveChangesAsync();
        var result = await new ReportService(new ReportRepository(context)).GetRevenueByMonthAsync(2026);
        Assert.True(result.Success);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Data));
        var months = json.RootElement.GetProperty("Data").EnumerateArray().ToArray();
        Assert.Equal(Enumerable.Range(1, 12), months.Select(x => x.GetProperty("Month").GetInt32()));
        Assert.Equal(350, months[0].GetProperty("Revenue").GetDecimal());
        Assert.Equal(2, months[0].GetProperty("TotalOrders").GetInt32());
        Assert.Equal(700, months[1].GetProperty("Revenue").GetDecimal());
        Assert.All(months.Skip(2), m => { Assert.Equal(0, m.GetProperty("Revenue").GetDecimal()); Assert.Equal(0, m.GetProperty("TotalOrders").GetInt32()); });
        Assert.DoesNotContain("InvalidOrders", JsonSerializer.Serialize(result.Data));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, -100)]
    [InlineData(false, 1000)]
    [InlineData(true, 0)]
    [InlineData(true, -100)]
    [InlineData(true, 1000)]
    public async Task Nonpositive_checkout_still_rolls_back_and_never_creates_order(bool counter, int price)
    {
        var work = new Mock<IUnitOfWork>();
        var orders = new Mock<IOrderRepository>();
        orders.Setup(x => x.GetBooksByIdsAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(new Dictionary<int, Book>
        { [1] = new Book { BookId = 1, IsActive = true, SellingPrice = price } });
        orders.Setup(x => x.TryIncreaseReservedQuantityAsync(1, 1)).ReturnsAsync(true);
        var promotions = new Mock<IPromotionRepository>();
        promotions.Setup(x => x.GetAllActiveAsync()).ReturnsAsync(Array.Empty<Promotion>());
        var members = new Mock<IMemberRepository>();
        members.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(new Member { MemberId = 1, Points = 10 });
        members.Setup(x => x.TryDeductPointsAsync(1, 1)).ReturnsAsync(true);
        var service = new OrderService(work.Object, orders.Object, promotions.Object, members.Object, Mock.Of<IPaymentRepository>());
        var points = price == 1000 ? 1 : 0;
        if (counter)
            await Assert.ThrowsAsync<ArgumentException>(() => service.CheckoutCounterAsync(new CounterCheckoutRequestDto
            { MemberId = 1, PointsToUse = points, Items = new() { new() { BookId = 1, Quantity = 1 } } }, 1));
        else
            await Assert.ThrowsAsync<ArgumentException>(() => service.CheckoutKioskAsync(new CheckoutRequestDto
            { MemberId = 1, PointsToUse = points, Items = new() { new() { BookId = 1, Quantity = 1 } } }));
        work.Verify(x => x.BeginTransactionAsync(), Times.Once);
        work.Verify(x => x.RollbackAsync(), Times.Once);
        work.Verify(x => x.CommitAsync(), Times.Never);
        orders.Verify(x => x.AddOrderAsync(It.IsAny<Order>()), Times.Never);
        if (points > 0) members.Verify(x => x.TryDeductPointsAsync(1, 1), Times.Once);
    }

    [Fact]
    public async Task Valid_simulation_remains_idempotent()
    {
        var order = new Order { OrderId = 1, OrderCode = "ORD-TEST", TotalAmount = 50000, OrderStatus = OrderStatus.Pending };
        var orders = new Mock<IOrderRepository>();
        orders.Setup(x => x.GetOrderByCodeAsync(order.OrderCode)).ReturnsAsync(order);
        orders.Setup(x => x.TryTransitionOrderStatusAsync(1, OrderStatus.Pending, OrderStatus.Paid, It.IsAny<DateTime?>())).ReturnsAsync(true);
        var work = new Mock<IUnitOfWork>();
        var payments = new Mock<IPaymentRepository>();
        var service = new PaymentService(work.Object, payments.Object, orders.Object, Mock.Of<IMemberRepository>());
        Assert.True((await service.SimulateDevSuccessAsync(order.OrderCode)).Success);
        Assert.True((await service.SimulateDevSuccessAsync(order.OrderCode)).Success);
        Assert.Equal(OrderStatus.Paid, order.OrderStatus);
        payments.Verify(x => x.AddTransactionAsync(It.IsAny<PaymentTransaction>()), Times.Once);
        work.Verify(x => x.CommitAsync(), Times.Once);
    }

    private static ApplicationDbContext NewContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static Order Paid(int id, decimal amount) => new() { OrderId = id, OrderCode = "ORD-" + id,
        TotalAmount = amount, OrderStatus = OrderStatus.Paid, CompletedAt = new DateTime(2026, 1, 1) };
}
