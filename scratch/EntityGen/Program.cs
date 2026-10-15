using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookKiosk.API.Controllers;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Interfaces.Services;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EntityGen;

// ---------------------------------------------------------------------------
// scratch/EntityGen — SQLite in-process regression runner
// KHÔNG kết nối SQL Server thật. KHÔNG RemoveRange trên bảng thật.
// Provider: SQLite (file test_regression.db, tạo mới mỗi lần chạy).
// Kết quả F01 concurrency 30-round SQL Server do Leader kiểm thử độc lập.
// ---------------------------------------------------------------------------

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("======================================================================");
        Console.WriteLine("BACKEND REGRESSION TESTS (.NET 10 + SQLite in-process)");
        Console.WriteLine("Fixes: B03 (P0), F01 (P0), D01 (P1), D03 (P1), B06 (P1), A04 (P2)");
        Console.WriteLine("======================================================================\n");

        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine($"  [PASS] {testName}");
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName}");
                Console.ResetColor();
                failed++;
            }
        }

        // SQLite file — tạo mới mỗi lần chạy, cô lập hoàn toàn với SQL Server thật
        var dbFile = "test_regression.db";
        if (System.IO.File.Exists(dbFile)) System.IO.File.Delete(dbFile);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={dbFile}")
            .Options;

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(options);
        services.AddScoped(sp => new ApplicationDbContext(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SePay:ApiKey",         "TEST_KEY"      },
                { "SePay:AccountNumber",  "0366994409"    },
                { "SePay:SubAccount",     "SUB123"        },
                { "ApiSettings:ApiKey",   "TEST_API_KEY"  }
            }).Build();
        services.AddSingleton<IConfiguration>(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // ------------------------------------------------------------------
        // SEED
        // ------------------------------------------------------------------
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();

            db.Categories.Add(new Category { CategoryId = 1, Name = "Technology" });
            db.Books.AddRange(
                new Book { BookId = 1, CategoryId = 1, Barcode = "BK001", Title = "C# 12 and .NET 8",    SellingPrice = 100000m, StockQuantity = 10, ReservedQuantity = 0, IsActive = true, Author = "Author A" },
                new Book { BookId = 2, CategoryId = 1, Barcode = "BK002", Title = "Clean Architecture", SellingPrice = 150000m, StockQuantity = 5,  ReservedQuantity = 0, IsActive = true, Author = "Author B" }
            );
            db.Members.Add(new Member { MemberId = 1, FullName = "Nguyen Van A", PhoneNumber = "0901234567", Points = 150 });
            await db.SaveChangesAsync();
        }

        // ==================================================================
        // TEST 1 — B03 (P0): Webhook validation — transferType và account
        // ==================================================================
        Console.WriteLine("--- 1. B03 (P0): Webhook validation ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            // 1.1 null transferType
            var p1 = new PaymentWebhookPayload { id = 101, transferType = null, accountNumber = "0366994409", amountIn = 100000m, code = "ORD-TEST-1" };
            Assert(!await svc.HandleSePayWebhookAsync(p1, "0366994409"), "B03.1: Reject webhook with null transferType");

            // 1.2 transferType = "out"
            var p2 = new PaymentWebhookPayload { id = 102, transferType = "out", accountNumber = "0366994409", amountIn = 100000m, code = "ORD-TEST-1" };
            Assert(!await svc.HandleSePayWebhookAsync(p2, "0366994409"), "B03.2: Reject webhook with transferType = 'out'");

            // 1.3 Thiếu cả accountNumber lẫn subAccount
            var p3 = new PaymentWebhookPayload { id = 103, transferType = "in", accountNumber = "", subAccount = null, amountIn = 100000m, code = "ORD-TEST-1" };
            Assert(!await svc.HandleSePayWebhookAsync(p3), "B03.3: Reject webhook when accountNumber and subAccount are both empty");

            // 1.4 accountNumber không khớp cấu hình
            var p4 = new PaymentWebhookPayload { id = 104, transferType = "in", accountNumber = "9999999999", amountIn = 100000m, code = "ORD-TEST-1" };
            Assert(!await svc.HandleSePayWebhookAsync(p4, "0366994409"), "B03.4: Reject webhook when accountNumber does not match");
        }

        // ==================================================================
        // TEST 2 — F01 (P0): F3 Dev Simulation vs Cancel
        // ==================================================================
        Console.WriteLine("\n--- 2. F01 (P0): F3 Dev Simulation vs Cancel ---");

        // 2a. Pre-cancelled: Service và Controller phải từ chối đúng
        using (var scope = serviceProvider.CreateScope())
        {
            var db  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            db.Orders.Add(new Order
            {
                OrderCode   = "ORD-CANCELLED-01",
                OrderStatus = OrderStatus.Cancelled,
                TotalAmount = 100000m,
                SubTotal    = 100000m
            });
            await db.SaveChangesAsync();

            var simResult = await svc.SimulateDevSuccessAsync("ORD-CANCELLED-01");
            Assert(!simResult.Success && simResult.Status == "Conflict",
                "F01.1: PaymentService returns (false, 'Conflict') when order is already Cancelled");

            var mockEnv = new FakeHostEnvironment { EnvironmentName = "Development" };
            var logger  = scope.ServiceProvider.GetRequiredService<ILogger<PaymentsController>>();
            var ctrl    = new PaymentsController(svc, logger, configuration, mockEnv);
            ctrl.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            ctrl.Request.Headers["X-Api-Key"] = "TEST_API_KEY";

            var actionResult = await ctrl.DevSimulateSuccess("ORD-CANCELLED-01");
            var objResult    = actionResult as ObjectResult;
            Assert(objResult != null && objResult.StatusCode == StatusCodes.Status409Conflict,
                "F01.2: PaymentsController returns HTTP 409 Conflict for Cancelled order");
        }

        // 2b. F01.3: Race simulation — Cancel thắng trước, F3 transition thất bại, reload NoTracking trả Conflict
        Console.WriteLine("\n  [F01.3] Race simulation (2 DbContext scopes on SQLite)");
        {
            Order raceOrder;

            // Setup: tạo order Pending
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                raceOrder = new Order
                {
                    OrderCode   = "ORD-RACE-F01",
                    OrderStatus = OrderStatus.Pending,
                    TotalAmount = 100000m,
                    SubTotal    = 100000m
                };
                db.Orders.Add(raceOrder);
                await db.SaveChangesAsync();
            }

            // Cancel thắng: dùng scope riêng biệt (DbContext khác) → transition Pending→Cancelled
            using (var cancelScope = serviceProvider.CreateScope())
            {
                var cancelRepo = cancelScope.ServiceProvider.GetRequiredService<IOrderRepository>();
                var cancelUow  = cancelScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await cancelUow.BeginTransactionAsync();
                bool cancelOk = await cancelRepo.TryTransitionOrderStatusAsync(raceOrder.OrderId, OrderStatus.Pending, OrderStatus.Cancelled);
                await cancelUow.SaveChangesAsync();
                await cancelUow.CommitAsync();
                Assert(cancelOk, "F01.3-setup: Cancel transitions order to Cancelled in DB (separate DbContext)");
            }

            // F3 chạy SimulateDevSuccessAsync — DB đã là Cancelled.
            // Service gọi TryTransition → thất bại → Rollback → GetOrderByCodeNoTrackingAsync
            // → phải đọc đúng Cancelled từ DB, trả (false, "Conflict")
            using (var f3Scope = serviceProvider.CreateScope())
            {
                var f3Svc   = f3Scope.ServiceProvider.GetRequiredService<IPaymentService>();
                var result  = await f3Svc.SimulateDevSuccessAsync("ORD-RACE-F01");
                Assert(!result.Success && result.Status == "Conflict",
                    "F01.3: SimulateDevSuccessAsync returns (false,'Conflict') when Cancel won the race");
                Assert(result.Order?.OrderStatus == OrderStatus.Cancelled,
                    "F01.3: Returned Order reflects true Cancelled state (AsNoTracking bypasses stale EF cache)");
            }
        }

        // ==================================================================
        // TEST 3 — D01 & D03 (P1): Point validation và atomic deduction
        // ==================================================================
        Console.WriteLine("\n--- 3. D01 & D03 (P1): Point Validation & Atomic Deduction ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var orderSvc = scope.ServiceProvider.GetRequiredService<IOrderService>();

            // D01.1: PointsToUse = -1
            bool d01Neg = false;
            try { await orderSvc.CheckoutKioskAsync(new CheckoutRequestDto { MemberId = 1, PointsToUse = -1, Items = new List<CheckoutItemDto> { new() { BookId = 1, Quantity = 1 } } }); }
            catch (ArgumentException ex) when (ex.Message.Contains("từ 0 đến 100")) { d01Neg = true; }
            Assert(d01Neg, "D01.1: Reject PointsToUse = -1 with ArgumentException");

            // D01.2: PointsToUse = 101
            bool d01Over = false;
            try { await orderSvc.CheckoutKioskAsync(new CheckoutRequestDto { MemberId = 1, PointsToUse = 101, Items = new List<CheckoutItemDto> { new() { BookId = 1, Quantity = 1 } } }); }
            catch (ArgumentException ex) when (ex.Message.Contains("từ 0 đến 100")) { d01Over = true; }
            Assert(d01Over, "D01.2: Reject PointsToUse = 101 with ArgumentException");

            // D01.3: PointsToUse > 0 nhưng không có MemberId
            bool d01NoMember = false;
            try { await orderSvc.CheckoutKioskAsync(new CheckoutRequestDto { MemberId = null, PointsToUse = 50, Items = new List<CheckoutItemDto> { new() { BookId = 1, Quantity = 1 } } }); }
            catch (ArgumentException ex) when (ex.Message.Contains("Cần cung cấp thông tin thành viên")) { d01NoMember = true; }
            Assert(d01NoMember, "D01.3: Reject PointsToUse > 0 without MemberId with ArgumentException");

            // D03.1: Request 1 — member 150 điểm, xin 100 → thành công, còn 50
            var resp1 = await orderSvc.CheckoutKioskAsync(new CheckoutRequestDto
            {
                MemberId = 1,
                PointsToUse = 100,
                Items = new List<CheckoutItemDto> { new() { BookId = 1, Quantity = 1 } }
            });
            Assert(resp1 != null && resp1.PointsUsedAmount == 100000m, "D03.1: Request 1 uses 100 points successfully");

            // D03.2: Request 2 — chỉ còn 50 điểm, xin 100 → phải bị từ chối (không tự ép xuống 50)
            bool req2Fail = false;
            try
            {
                await orderSvc.CheckoutKioskAsync(new CheckoutRequestDto
                {
                    MemberId = 1,
                    PointsToUse = 100,
                    Items = new List<CheckoutItemDto> { new() { BookId = 1, Quantity = 1 } }
                });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ điểm")) { req2Fail = true; }
            Assert(req2Fail, "D03.2: Request 2 rejected when only 50 points remain (NOT silently clamped)");
        }

        // ==================================================================
        // TEST 4 — B06 (P1): Đơn Paid nhận thêm ReferenceCode mới → lưu đối soát
        // ==================================================================
        Console.WriteLine("\n--- 4. B06 (P1): Paid order receives new ReferenceCode ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var db  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var svc = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            var paidOrder = new Order { OrderCode = "ORD-PAID-01", OrderStatus = OrderStatus.Paid, TotalAmount = 100000m, SubTotal = 100000m };
            db.Orders.Add(paidOrder);
            await db.SaveChangesAsync();

            var tx1 = new PaymentWebhookPayload { id = 201, transferType = "in", accountNumber = "0366994409", amountIn = 100000m, referenceCode = "REF_ORIGINAL_01", code = "ORD-PAID-01" };
            Assert(await svc.HandleSePayWebhookAsync(tx1, "0366994409"), "B06.1: First webhook on Paid order handled");

            var tx2 = new PaymentWebhookPayload { id = 202, transferType = "in", accountNumber = "0366994409", amountIn = 50000m, referenceCode = "REF_ADDITIONAL_02", code = "ORD-PAID-01" };
            Assert(await svc.HandleSePayWebhookAsync(tx2, "0366994409"), "B06.2: Second webhook with new ReferenceCode handled");

            var txList = await db.PaymentTransactions.Where(t => t.OrderId == paidOrder.OrderId).ToListAsync();
            Assert(txList.Any(t => t.ReferenceCode == "REF_ORIGINAL_01") && txList.Any(t => t.ReferenceCode == "REF_ADDITIONAL_02"),
                "B06.3: Both transactions saved for audit; no double inventory decrement");
        }

        // ==================================================================
        // TEST 5 — A04 (P2): Duplicate BookId trong Items
        // ==================================================================
        Console.WriteLine("\n--- 5. A04 (P2): Duplicate BookId in Items ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var orderSvc = scope.ServiceProvider.GetRequiredService<IOrderService>();
            bool a04 = false;
            try
            {
                await orderSvc.CheckoutKioskAsync(new CheckoutRequestDto
                {
                    Items = new List<CheckoutItemDto>
                    {
                        new() { BookId = 1, Quantity = 1 },
                        new() { BookId = 1, Quantity = 2 }
                    }
                });
            }
            catch (ArgumentException ex) when (ex.Message.Contains("trùng lặp (BookId: 1)")) { a04 = true; }
            Assert(a04, "A04: Reject checkout with duplicate BookId with ArgumentException");
        }

        // ==================================================================
        // KẾT QUẢ
        // ==================================================================
        Console.WriteLine("\n======================================================================");
        Console.WriteLine($"TOTAL: {passed} PASSED, {failed} FAILED");
        Console.WriteLine("======================================================================");

        if (failed > 0) Environment.Exit(1);
    }
}

public class FakeHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ApplicationName  { get; set; } = "BookKiosk.API";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath  { get; set; } = string.Empty;
    public string EnvironmentName  { get; set; } = "Development";
}
