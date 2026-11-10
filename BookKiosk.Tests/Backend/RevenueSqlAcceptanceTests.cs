using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Tests.Backend;

public sealed class RevenueSqlAcceptanceTests(DisposableSqlQa db, Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<DisposableSqlQa>
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

    [SqlQaFact]
    public async Task Corrupt_pending_and_paid_replays_cannot_write_ledger_or_change_stock_in_sql()
    {
        var book = await db.AddBookAsync();
        await using var context = db.Context();
        using var work = new UnitOfWork(context);
        var orders = new OrderRepository(context);
        var service = new BookKiosk.Application.Services.PaymentService(work, new PaymentRepository(context), orders, new MemberRepository(context));
        foreach (var status in new[] {OrderStatus.Pending, OrderStatus.Paid})
        foreach (var amount in new[] {0m, -100m})
        {
            var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = amount,
                OrderStatus = status, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR,
                CompletedAt = status == OrderStatus.Paid ? DateTime.UtcNow : null,
                OrderDetails = new List<OrderDetail> { new() { BookId = book.BookId, Quantity = 1, UnitPriceAtTime = 1000, LineTotal = 1000 } } };
            context.Orders.Add(row); await context.SaveChangesAsync();
            if(status == OrderStatus.Pending)
                Assert.False(await orders.TryTransitionOrderStatusAsync(row.OrderId, OrderStatus.Pending, OrderStatus.Paid, DateTime.UtcNow));
            Assert.False(await service.HandleSePayWebhookAsync(new()
            { referenceCode = Guid.NewGuid().ToString("N"), transferType = "in", accountNumber = "QA", code = row.OrderCode, transferAmount = amount }));
            Assert.False((await service.SimulateDevSuccessAsync(row.OrderCode)).Success);
            Assert.False(await context.PaymentTransactions.AnyAsync(x => x.OrderId == row.OrderId));
            var persisted = await context.Orders.AsNoTracking().SingleAsync(x => x.OrderId == row.OrderId);
            Assert.Equal(status, persisted.OrderStatus); Assert.Equal(amount, persisted.TotalAmount);
        }
        var good = new Order { OrderCode = "ORD-"+Guid.NewGuid().ToString("N"), TotalAmount = 1000,
            OrderStatus = OrderStatus.Paid, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR, CompletedAt = DateTime.UtcNow,
            OrderDetails = new List<OrderDetail> { new() { BookId = book.BookId, Quantity = 1, UnitPriceAtTime = 1000, LineTotal = 1000 } } };
        context.Orders.Add(good); await context.SaveChangesAsync();
        var payload = new BookKiosk.Application.Services.PaymentWebhookPayload
        {referenceCode=Guid.NewGuid().ToString("N"),transferType="in",accountNumber="QA",code=good.OrderCode,transferAmount=1000};
        Assert.True(await service.HandleSePayWebhookAsync(payload));
        Assert.True(await service.HandleSePayWebhookAsync(payload));
        Assert.True((await service.SimulateDevSuccessAsync(good.OrderCode)).Success);
        Assert.Equal(1,await context.PaymentTransactions.CountAsync(x=>x.OrderId==good.OrderId));
        var stock=await context.Books.AsNoTracking().SingleAsync(x=>x.BookId==book.BookId);
        Assert.Equal(10,stock.StockQuantity);Assert.Equal(0,stock.ReservedQuantity);
        Assert.Null(context.Database.CurrentTransaction);
        await work.BeginTransactionAsync();await work.RollbackAsync();
        Assert.Null(context.Database.CurrentTransaction);
    }

    [SqlQaFact]
    public async Task Amount_races_fail_closed_without_tracked_ledger_and_scope_can_recover()
    {
        foreach (var simulate in new[] { false, true })
        foreach (var invalidAmount in new[] { 0m, -100m })
        {
            var book = await db.AddBookAsync();
            await using var seed = db.Context();
            await seed.Books.Where(x => x.BookId == book.BookId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservedQuantity, 1));
            var member = new Member { PhoneNumber = Guid.NewGuid().ToString("N")[..10], FullName = "QA race", Points = 7 };
            var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = 10000,
                OrderStatus = OrderStatus.Pending, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR,
                Member = member, OrderDetails = new List<OrderDetail> { new() { BookId = book.BookId, Quantity = 1, UnitPriceAtTime = 10000, LineTotal = 10000 } } };
            seed.Orders.Add(row); await seed.SaveChangesAsync();
            var gate = new BeforePaidTransition(async () =>
            {
                await using var other = db.Context();
                await other.Orders.Where(x => x.OrderId == row.OrderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalAmount, invalidAmount));
            });
            await using var context = db.Context(gate);
            using var work = new UnitOfWork(context);
            var service = new BookKiosk.Application.Services.PaymentService(work, new PaymentRepository(context), new OrderRepository(context), new MemberRepository(context));
            var payload = new BookKiosk.Application.Services.PaymentWebhookPayload { referenceCode = Guid.NewGuid().ToString("N"),
                transferType = "in", accountNumber = "QA", code = row.OrderCode, transferAmount = 10000 };
            Assert.False(simulate ? (await service.SimulateDevSuccessAsync(row.OrderCode)).Success : await service.HandleSePayWebhookAsync(payload));
            Assert.True(gate.Triggered);
            Assert.DoesNotContain(context.ChangeTracker.Entries<PaymentTransaction>(), x => x.State == EntityState.Added);
            // A later SaveChanges in the same scope must not resurrect a rejected ledger entry.
            await work.SaveChangesAsync(); await work.BeginTransactionAsync(); await work.RollbackAsync();
            Assert.Null(context.Database.CurrentTransaction);
            await using var verify = db.Context();
            var persisted = await verify.Orders.AsNoTracking().SingleAsync(x => x.OrderId == row.OrderId);
            Assert.Equal(OrderStatus.Pending, persisted.OrderStatus); Assert.Null(persisted.CompletedAt); Assert.Equal(invalidAmount, persisted.TotalAmount);
            Assert.False(await verify.PaymentTransactions.AnyAsync(x => x.OrderId == row.OrderId));
            Assert.False(await verify.PointTransactions.AnyAsync(x => x.OrderId == row.OrderId));
            Assert.Equal(7, (await verify.Members.SingleAsync(x => x.MemberId == member.MemberId)).Points);
            var stock = await verify.Books.SingleAsync(x => x.BookId == book.BookId);
            Assert.Equal(10, stock.StockQuantity); Assert.Equal(1, stock.ReservedQuantity);
        }
    }

    [SqlQaFact]
    public async Task Positive_status_races_preserve_reconciliation_and_duplicate_idempotency()
    {
        foreach (var status in new[] { OrderStatus.Paid, OrderStatus.Cancelled })
        {
            await using var seed = db.Context();
            var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = 1000,
                OrderStatus = OrderStatus.Pending, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR };
            seed.Orders.Add(row); await seed.SaveChangesAsync();
            var gate = new BeforePaidTransition(async () =>
            {
                await using var other = db.Context();
                await other.Orders.Where(x => x.OrderId == row.OrderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.OrderStatus, status));
            });
            await using var context = db.Context(gate);
            using var work = new UnitOfWork(context);
            var service = new BookKiosk.Application.Services.PaymentService(work, new PaymentRepository(context), new OrderRepository(context), new MemberRepository(context));
            var payload = new BookKiosk.Application.Services.PaymentWebhookPayload { referenceCode = Guid.NewGuid().ToString("N"), transferType = "in", accountNumber = "QA", code = row.OrderCode, transferAmount = 1000 };
            Assert.True(await service.HandleSePayWebhookAsync(payload));
            Assert.True(gate.Triggered);
            Assert.True(await service.HandleSePayWebhookAsync(payload));
            await using var verify = db.Context();
            Assert.Equal(status, (await verify.Orders.AsNoTracking().SingleAsync(x => x.OrderId == row.OrderId)).OrderStatus);
            Assert.Equal(1, await verify.PaymentTransactions.CountAsync(x => x.OrderId == row.OrderId));
            Assert.False(await verify.PointTransactions.AnyAsync(x => x.OrderId == row.OrderId));
        }
    }

    private sealed class BeforePaidTransition(Func<Task> action) : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Triggered && command.CommandText.Contains("UPDATE") && command.CommandText.Contains("[Orders]") && command.CommandText.Contains("[OrderStatus]"))
            {
                Triggered = true;
                await action();
            }
            return result;
        }
    }

    [SqlQaFact]
    public async Task Readonly_actual_qa_schema_and_synthetic_row_audit_is_recorded()
    {
        await using var context=db.Context();
        await context.Database.OpenConnectionAsync();
        await using var command=context.Database.GetDbConnection().CreateCommand();
        command.CommandText="SELECT DB_NAME() AS QaDatabase, (SELECT OBJECT_NAME(parent_object_id) AS TableName,name,definition,is_disabled,is_not_trusted FROM sys.check_constraints WHERE parent_object_id IN (OBJECT_ID('dbo.Orders'),OBJECT_ID('dbo.PaymentTransactions')) FOR JSON PATH) AS ConstraintsJson, (SELECT OBJECT_NAME(object_id) AS TableName,name AS ColumnName,TYPE_NAME(user_type_id) AS SqlType,precision,scale,is_nullable FROM sys.columns WHERE (object_id=OBJECT_ID('dbo.Orders') AND name='TotalAmount') OR (object_id=OBJECT_ID('dbo.PaymentTransactions') AND name='Amount') FOR JSON PATH) AS ColumnsJson, (SELECT OrderStatus,COUNT_BIG(*) AS TotalRows,SUM(CASE WHEN TotalAmount<=0 THEN CONVERT(bigint,1) ELSE 0 END) AS NonPositiveRows FROM dbo.Orders GROUP BY OrderStatus FOR JSON PATH) AS SyntheticRowsJson";
        await using var reader=await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var audit=new Dictionary<string,object?>();
        for(int i=0;i<reader.FieldCount;i++) audit[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);
        Assert.StartsWith("BookKiosk_QA_",(string)audit["QaDatabase"]!);
        output.WriteLine("Disposable QA schema audit; synthetic fixture data, not production legacy evidence: "+JsonSerializer.Serialize(audit));
    }

}
