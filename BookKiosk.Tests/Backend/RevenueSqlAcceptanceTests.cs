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

    [SqlQaFact]
    public Task Mismatched_webhook_rejects_zero_persisted_amount_race() => VerifyMismatchRace(0);

    [SqlQaFact]
    public Task Mismatched_webhook_rejects_negative_persisted_amount_race() => VerifyMismatchRace(-100);

    private async Task VerifyMismatchRace(decimal invalidAmount)
    {
        foreach (var status in new[] { OrderStatus.Pending, OrderStatus.Paid, OrderStatus.Cancelled })
        {
            var book = await db.AddBookAsync();
            await using var seed = db.Context();
            await seed.Books.Where(x => x.BookId == book.BookId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservedQuantity, 1));
            var member = new Member { PhoneNumber = Guid.NewGuid().ToString("N")[..10], FullName = "Mismatch QA", Points = 9 };
            var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = 1000,
                OrderStatus = status, Member = member, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR,
                CompletedAt = status == OrderStatus.Paid ? DateTime.UtcNow : null,
                OrderDetails = new List<OrderDetail> { new() { BookId = book.BookId, Quantity = 1, UnitPriceAtTime = 1000, LineTotal = 1000 } } };
            seed.Orders.Add(row); await seed.SaveChangesAsync();
            var gate = new BeforeDuplicateCheck(async () =>
            {
                await using var other = db.Context();
                await other.Orders.Where(x => x.OrderId == row.OrderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalAmount, invalidAmount));
            });
            await using var context = db.Context(gate);
            using var work = new UnitOfWork(context);
            var service = new BookKiosk.Application.Services.PaymentService(work, new PaymentRepository(context), new OrderRepository(context), new MemberRepository(context));
            Assert.False(await service.HandleSePayWebhookAsync(new() { referenceCode = Guid.NewGuid().ToString("N"),
                transferType = "in", accountNumber = "QA", code = row.OrderCode, transferAmount = 1500 }));
            Assert.True(gate.Triggered);
            Assert.DoesNotContain(context.ChangeTracker.Entries<PaymentTransaction>(), x => x.State == EntityState.Added);
            await work.SaveChangesAsync(); await work.BeginTransactionAsync(); await work.RollbackAsync();
            Assert.Null(context.Database.CurrentTransaction);
            await using var verify = db.Context();
            var persisted = await verify.Orders.AsNoTracking().SingleAsync(x => x.OrderId == row.OrderId);
            Assert.Equal(status, persisted.OrderStatus); Assert.Equal(invalidAmount, persisted.TotalAmount); Assert.Equal(row.CompletedAt, persisted.CompletedAt);
            Assert.False(await verify.PaymentTransactions.AnyAsync(x => x.OrderId == row.OrderId));
            Assert.False(await verify.PointTransactions.AnyAsync(x => x.OrderId == row.OrderId));
            Assert.Equal(9, (await verify.Members.SingleAsync(x => x.MemberId == member.MemberId)).Points);
            var stock = await verify.Books.SingleAsync(x => x.BookId == book.BookId);
            Assert.Equal(10, stock.StockQuantity); Assert.Equal(1, stock.ReservedQuantity);
            output.WriteLine($"Invalid mismatch race: status={status}, persistedAmount={invalidAmount}, accepted=false, ledger=0, stock=10, reserved=1, points=9");
        }
    }

    [SqlQaFact]
    public async Task Valid_mismatch_reconciles_once_without_mutating_pending_stock_or_points()
    {
        var book = await db.AddBookAsync();
        await using var context = db.Context();
        var member = new Member { PhoneNumber = Guid.NewGuid().ToString("N")[..10], FullName = "Valid mismatch", Points = 9 };
        var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = 1000, OrderStatus = OrderStatus.Pending,
            Member = member, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR,
            OrderDetails = new List<OrderDetail> { new() { BookId = book.BookId, Quantity = 1, UnitPriceAtTime = 1000, LineTotal = 1000 } } };
        context.Orders.Add(row); await context.SaveChangesAsync();
        using var work = new UnitOfWork(context);
        var service = new BookKiosk.Application.Services.PaymentService(work, new PaymentRepository(context), new OrderRepository(context), new MemberRepository(context));
        var payload = new BookKiosk.Application.Services.PaymentWebhookPayload { referenceCode = Guid.NewGuid().ToString("N"), transferType = "in", accountNumber = "QA", code = row.OrderCode, transferAmount = 1500 };
        Assert.True(await service.HandleSePayWebhookAsync(payload)); Assert.True(await service.HandleSePayWebhookAsync(payload));
        await using var verify = db.Context();
        var tx = Assert.Single(await verify.PaymentTransactions.Where(x => x.OrderId == row.OrderId).ToListAsync()); Assert.Equal(1500, tx.Amount);
        Assert.Equal(OrderStatus.Pending, (await verify.Orders.AsNoTracking().SingleAsync(x => x.OrderId == row.OrderId)).OrderStatus);
        Assert.Equal(10, (await verify.Books.SingleAsync(x => x.BookId == book.BookId)).StockQuantity);
        Assert.Equal(9, (await verify.Members.SingleAsync(x => x.MemberId == member.MemberId)).Points);
        Assert.False(await verify.PointTransactions.AnyAsync(x => x.OrderId == row.OrderId));
    }

    [SqlQaFact]
    public async Task Reconciliation_lock_blocks_amount_update_and_releases_after_commit_or_rollback()
    {
        foreach (var commit in new[] { false, true })
        {
            await using var context = db.Context();
            var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = 1000,
                OrderStatus = OrderStatus.Pending, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR };
            context.Orders.Add(row); await context.SaveChangesAsync();
            using var work = new UnitOfWork(context);
            var orders = new OrderRepository(context);
            await Assert.ThrowsAsync<InvalidOperationException>(() => orders.GetOrderForPaymentReconciliationAsync(row.OrderCode));
            await work.BeginTransactionAsync();
            Assert.Equal(1000, (await orders.GetOrderForPaymentReconciliationAsync(row.OrderCode))!.TotalAmount);
            await using var other = db.Context();
            await other.Database.OpenConnectionAsync();
            await using var command = other.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SET LOCK_TIMEOUT 300; UPDATE [Orders] SET [TotalAmount]=0 WHERE [OrderId]=@id";
            var id = command.CreateParameter(); id.ParameterName = "@id"; id.Value = row.OrderId; command.Parameters.Add(id);
            var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(1222, exception.Number);
            if (commit) await work.CommitAsync(); else await work.RollbackAsync();
            command.CommandText = "SET LOCK_TIMEOUT 5000; UPDATE [Orders] SET [TotalAmount]=0 WHERE [OrderId]=@id";
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
            Assert.Null(context.Database.CurrentTransaction);
            output.WriteLine($"Reconciliation lock: concurrent invalidation blocked; released after {(commit ? "commit" : "rollback")}");
        }
    }

    [SqlQaFact]
    public async Task Positive_upward_drift_rejects_matching_webhook_without_side_effects()
    {
        await VerifyPositiveDrift("webhook", 1000, 1500);
        await VerifyPositiveDrift("webhook", 10000, 15000);
    }

    [SqlQaFact]
    public async Task Positive_downward_drift_rejects_matching_webhook_without_side_effects()
    {
        await VerifyPositiveDrift("webhook", 1000, 500);
        await VerifyPositiveDrift("webhook", 10000, 5000);
    }

    [SqlQaFact]
    public async Task Positive_drift_cannot_pay_through_dev_or_cash_paths()
    {
        await VerifyPositiveDrift("dev", 10000, 15000);
        await VerifyPositiveDrift("cash", 10000, 15000);
    }

    [SqlQaFact]
    public Task Snapshot_mismatch_becoming_persisted_match_remains_reconciliation_only()
        => VerifyPositiveDrift("mismatch", 1000, 1500);

    private async Task VerifyPositiveDrift(string flow, decimal original, decimal changed)
    {
        var book = await db.AddBookAsync();
        await using var seed = db.Context();
        await seed.Books.Where(x => x.BookId == book.BookId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReservedQuantity, 1));
        var member = new Member { PhoneNumber = Guid.NewGuid().ToString("N")[..10], FullName = "Drift QA", Points = 7 };
        var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = original,
            OrderStatus = OrderStatus.Pending, Member = member, SaleChannel = SaleChannel.Counter,
            PaymentMethod = flow == "cash" ? PaymentMethod.Cash : PaymentMethod.QR,
            OrderDetails = new List<OrderDetail> { new() { BookId = book.BookId, Quantity = 1, UnitPriceAtTime = original, LineTotal = original } } };
        seed.Orders.Add(row); await seed.SaveChangesAsync();
        async Task Drift()
        {
            await using var writer = db.Context();
            await writer.Orders.Where(x => x.OrderId == row.OrderId).ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalAmount, changed));
        }
        var updateGate = new BeforePaidTransition(Drift);
        var duplicateGate = new BeforeDuplicateCheck(Drift);
        var reconciliationOnly = flow == "mismatch";
        await using var context = db.Context(reconciliationOnly ? duplicateGate : updateGate);
        using var work = new UnitOfWork(context);
        var orders = new OrderRepository(context);
        var payments = new BookKiosk.Application.Services.PaymentService(work, new PaymentRepository(context), orders, new MemberRepository(context));
        if (flow == "cash")
        {
            var service = new BookKiosk.Application.Services.OrderService(work, orders, new PromotionRepository(context), new MemberRepository(context), new PaymentRepository(context));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmCounterPaymentAsync(row.OrderId, 33));
        }
        else if (flow == "dev") Assert.False((await payments.SimulateDevSuccessAsync(row.OrderCode)).Success);
        else
        {
            var payload = new BookKiosk.Application.Services.PaymentWebhookPayload { referenceCode = Guid.NewGuid().ToString("N"), transferType = "in",
                accountNumber = "QA", code = row.OrderCode, transferAmount = reconciliationOnly ? changed : original };
            Assert.Equal(reconciliationOnly, await payments.HandleSePayWebhookAsync(payload));
            Assert.Equal(reconciliationOnly, await payments.HandleSePayWebhookAsync(payload));
        }
        Assert.True(reconciliationOnly ? duplicateGate.Triggered : updateGate.Triggered);
        Assert.DoesNotContain(context.ChangeTracker.Entries<PaymentTransaction>(), x => x.State == EntityState.Added);
        await work.SaveChangesAsync(); await work.BeginTransactionAsync(); await work.RollbackAsync();
        Assert.Null(context.Database.CurrentTransaction);
        await using var verify = db.Context();
        var persisted = await verify.Orders.AsNoTracking().SingleAsync(x => x.OrderId == row.OrderId);
        Assert.Equal(OrderStatus.Pending, persisted.OrderStatus); Assert.Null(persisted.CompletedAt); Assert.Equal(changed, persisted.TotalAmount);
        Assert.Equal(reconciliationOnly ? 1 : 0, await verify.PaymentTransactions.CountAsync(x => x.OrderId == row.OrderId));
        Assert.False(await verify.PointTransactions.AnyAsync(x => x.OrderId == row.OrderId));
        Assert.Equal(7, (await verify.Members.SingleAsync(x => x.MemberId == member.MemberId)).Points);
        var stock = await verify.Books.SingleAsync(x => x.BookId == book.BookId);
        Assert.Equal(10, stock.StockQuantity); Assert.Equal(1, stock.ReservedQuantity);
        output.WriteLine($"Positive drift: flow={flow}, original={original}, persisted={changed}, Pending=true, ledger={(reconciliationOnly ? 1 : 0)}, stock=10, reserved=1, points=7");
    }

    [SqlQaFact]
    public async Task Atomic_paid_amount_is_locked_through_ledger_commit_or_rollback()
    {
        foreach (var commit in new[] { false, true })
        {
            await using var context = db.Context();
            var row = new Order { OrderCode = "ORD-" + Guid.NewGuid().ToString("N"), TotalAmount = 1000,
                OrderStatus = OrderStatus.Pending, SaleChannel = SaleChannel.Counter, PaymentMethod = PaymentMethod.QR };
            context.Orders.Add(row); await context.SaveChangesAsync();
            using var work = new UnitOfWork(context);
            var orders = new OrderRepository(context);
            await work.BeginTransactionAsync();
            Assert.False(await orders.TryPayOrderAsync(row.OrderId, 1500, DateTime.UtcNow));
            Assert.True(await orders.TryPayOrderAsync(row.OrderId, 1000, DateTime.UtcNow));
            await new PaymentRepository(context).AddTransactionAsync(new PaymentTransaction { OrderId = row.OrderId,
                ReferenceCode = Guid.NewGuid().ToString("N"), Amount = 1000, Gateway = "QA" });
            await work.SaveChangesAsync();
            await using var other = db.Context();
            await other.Database.OpenConnectionAsync();
            await using var command = other.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SET LOCK_TIMEOUT 300; UPDATE [Orders] SET [TotalAmount]=1500 WHERE [OrderId]=@id";
            var id = command.CreateParameter(); id.ParameterName = "@id"; id.Value = row.OrderId; command.Parameters.Add(id);
            var error = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(1222, error.Number);
            if (commit) await work.CommitAsync(); else await work.RollbackAsync();
            command.CommandText = "SET LOCK_TIMEOUT 5000; SELECT [TotalAmount] FROM [Orders] WITH (UPDLOCK) WHERE [OrderId]=@id";
            Assert.Equal(1000m, Convert.ToDecimal(await command.ExecuteScalarAsync()));
            var persisted = await other.Orders.AsNoTracking().SingleAsync(x => x.OrderId == row.OrderId);
            Assert.Equal(commit ? OrderStatus.Paid : OrderStatus.Pending, persisted.OrderStatus);
            Assert.Equal(commit ? 1 : 0, await other.PaymentTransactions.CountAsync(x => x.OrderId == row.OrderId));
            Assert.Null(context.Database.CurrentTransaction);
            output.WriteLine($"Atomic Paid equality + ledger: competing amount UPDATE blocked; {(commit ? "commit" : "rollback")} coherent, total=1000");
        }
    }

    private sealed class BeforeDuplicateCheck(Func<Task> action) : DbCommandInterceptor
    {
        public bool Triggered { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!Triggered && command.CommandText.Contains("SELECT") && command.CommandText.Contains("[PaymentTransactions]"))
            {
                Triggered = true;
                await action();
            }
            return result;
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
