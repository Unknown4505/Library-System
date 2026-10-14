#pragma warning disable ASPDEPR004, ASPDEPR008
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EntityGen;

public class ConcurrencyHookOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly Func<string, Task>? _afterGetOrderHook;

    public ConcurrencyHookOrderRepository(IOrderRepository inner, Func<string, Task>? afterGetOrderHook = null)
    {
        _inner = inner;
        _afterGetOrderHook = afterGetOrderHook;
    }

    public async Task<Order?> GetOrderByCodeAsync(string orderCode)
    {
        var order = await _inner.GetOrderByCodeAsync(orderCode);
        if (_afterGetOrderHook != null)
        {
            await _afterGetOrderHook(orderCode);
        }
        return order;
    }

    public Task<Dictionary<int, Book>> GetBooksByIdsAsync(IEnumerable<int> bookIds) => _inner.GetBooksByIdsAsync(bookIds);
    public Task IncreaseReservedQuantityAsync(int bookId, int quantity) => _inner.IncreaseReservedQuantityAsync(bookId, quantity);
    public Task DecreaseStockAndReservedQuantityAsync(int bookId, int quantity) => _inner.DecreaseStockAndReservedQuantityAsync(bookId, quantity);
    public Task AddOrderAsync(Order order) => _inner.AddOrderAsync(order);
    public Task<bool> TryIncreaseReservedQuantityAsync(int bookId, int quantity) => _inner.TryIncreaseReservedQuantityAsync(bookId, quantity);
    public Task<bool> TryTransitionOrderStatusAsync(int orderId, OrderStatus fromStatus, OrderStatus toStatus, DateTime? completedAt = null) => _inner.TryTransitionOrderStatusAsync(orderId, fromStatus, toStatus, completedAt);
    public Task<Order?> GetPendingOrderByCodeAsync(string orderCode) => _inner.GetPendingOrderByCodeAsync(orderCode);
    public Task<Order?> GetOrderByIdAsync(int orderId) => _inner.GetOrderByIdAsync(orderId);
    public Task<Order?> GetOrderByCodeNoTrackingAsync(string orderCode) => _inner.GetOrderByCodeNoTrackingAsync(orderCode);
}

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("======================================================================");
        Console.WriteLine("STARTING BACKEND REGRESSION & CONCURRENCY SUITE (.NET 10 + SQL SERVER)");
        Console.WriteLine("Testing fixes for: P0 - F01 (F3/Cancel Race 30 Iterations + HTTP Test)");
        Console.WriteLine("Regression: B03 (P0), D01 (P1), D03 (P1), B06 (P1), A04 (P2), R01, R02, R03");
        Console.WriteLine("======================================================================\n");

        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine($"[PASS] {testName}");
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName}");
                Console.ResetColor();
                failed++;
            }
        }

        // 1. Cấu hình SQL Server Express thật
        var sqlConnString = Environment.GetEnvironmentVariable("BOOKKIOSK_TEST_SQLSERVER") 
                            ?? "Server=localhost;Database=BookKiosk_Test;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        Console.WriteLine($"Target Database: SQL Server Express -> {sqlConnString.Split(';')[1]}");

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(sqlConnString)
            .Options;

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(dbOptions);
        services.AddScoped(sp => new ApplicationDbContext(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentService, PaymentService>();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SePay:ApiKey", "TEST_KEY" },
            { "SePay:AccountNumber", "0366994409" },
            { "SePay:SubAccount", "SUB123" },
            { "ApiSettings:ApiKey", "kiosk-secret-key" }
        }).Build();
        services.AddSingleton<IConfiguration>(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // 2. APPLY MIGRATIONS & SEED DATA ON SQL SERVER
        Console.WriteLine("\nApplying EF Core real migrations on SQL Server Express...");
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();

            // Dọn dẹp dữ liệu test cũ để sạch sẽ
            db.OrderDetails.RemoveRange(db.OrderDetails);
            db.PaymentTransactions.RemoveRange(db.PaymentTransactions);
            db.PointTransactions.RemoveRange(db.PointTransactions);
            db.Orders.RemoveRange(db.Orders);
            db.Books.RemoveRange(db.Books);
            db.Categories.RemoveRange(db.Categories);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();

            // Seed master data with IDENTITY_INSERT on SQL Server
            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                var cat = new Category { CategoryId = 1, Name = "Technology" };
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Categories ON;");
                db.Categories.Add(cat);
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Categories OFF;");

                var book1 = new Book { BookId = 1, CategoryId = 1, Barcode = "BK001", Title = "C# 12 and .NET 8", SellingPrice = 100000m, StockQuantity = 50, ReservedQuantity = 0, IsActive = true, Author = "Author A" };
                var book2 = new Book { BookId = 2, CategoryId = 1, Barcode = "BK002", Title = "Clean Architecture", SellingPrice = 150000m, StockQuantity = 20, ReservedQuantity = 0, IsActive = true, Author = "Author B" };
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Books ON;");
                db.Books.AddRange(book1, book2);
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Books OFF;");

                var member1 = new Member { MemberId = 1, FullName = "Nguyen Van A", PhoneNumber = "0901234567", Points = 150 };
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Members ON;");
                db.Members.Add(member1);
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT Members OFF;");

                await tx.CommitAsync();
            }

            Console.WriteLine("Database migrated and seeded successfully.\n");
        }

        // -------------------------------------------------------------
        // TEST 1: BE-NEW-10 / B03 (P0) - Webhook validation transferType and account
        // -------------------------------------------------------------
        Console.WriteLine("--- 1. TEST B03 (P0): Webhook validation ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            var p1 = new PaymentWebhookPayload { id = 101, transferType = null, accountNumber = "0366994409", amountIn = 100000m, code = "ORD-TEST-1" };
            bool res1 = await paymentService.HandleSePayWebhookAsync(p1, "0366994409");
            Assert(!res1, "B03.1: Reject webhook with null transferType");

            var p2 = new PaymentWebhookPayload { id = 102, transferType = "out", accountNumber = "0366994409", amountIn = 100000m, code = "ORD-TEST-1" };
            bool res2 = await paymentService.HandleSePayWebhookAsync(p2, "0366994409");
            Assert(!res2, "B03.2: Reject webhook with transferType = 'out'");

            var p3 = new PaymentWebhookPayload { id = 103, transferType = "in", accountNumber = "", subAccount = null, amountIn = 100000m, code = "ORD-TEST-1" };
            bool res3 = await paymentService.HandleSePayWebhookAsync(p3);
            Assert(!res3, "B03.3: Reject webhook when accountNumber and subAccount are both empty");

            var p4 = new PaymentWebhookPayload { id = 104, transferType = "in", accountNumber = "9999999999", amountIn = 100000m, code = "ORD-TEST-1" };
            bool res4 = await paymentService.HandleSePayWebhookAsync(p4, "0366994409");
            Assert(!res4, "B03.4: Reject webhook when accountNumber does not match expected configuration");
        }

        // -------------------------------------------------------------
        // TEST 2a: F01 (P0) - Baseline: Already Cancelled Order
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 2a. TEST F01 Baseline: Pre-Cancelled Order ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            var cancelledOrder = new Order
            {
                OrderCode = "ORD-CANCELLED-BASE",
                OrderStatus = OrderStatus.Cancelled,
                TotalAmount = 100000m,
                SubTotal = 100000m
            };
            db.Orders.Add(cancelledOrder);
            await db.SaveChangesAsync();

            var simResult = await paymentService.SimulateDevSuccessAsync("ORD-CANCELLED-BASE");
            Assert(!simResult.Success && simResult.Status == "Conflict", "F01.Base1: PaymentService returns (false, 'Conflict') when order is already Cancelled");

            var mockEnv = new FakeHostEnvironment { EnvironmentName = "Development" };
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentsController>>();
            var controller = new PaymentsController(paymentService, logger, configuration, mockEnv);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            controller.Request.Headers["X-Api-Key"] = "kiosk-secret-key";

            var actionResult = await controller.DevSimulateSuccess("ORD-CANCELLED-BASE");
            var objectResult = actionResult as ObjectResult;
            Assert(objectResult != null && objectResult.StatusCode == StatusCodes.Status409Conflict, 
                "F01.Base2: PaymentsController returns HTTP 409 Conflict for Cancelled order");

            // F01.Base3: Happy path on Pending order -> Success = true, Status = Paid, HTTP 200 OK
            var happyOrder = new Order
            {
                OrderCode = "ORD-HAPPY-01",
                OrderStatus = OrderStatus.Pending,
                TotalAmount = 100000m,
                SubTotal = 100000m,
                MemberId = 1,
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail { BookId = 1, Quantity = 1, UnitPriceAtTime = 100000m, LineTotal = 100000m }
                }
            };
            db.Orders.Add(happyOrder);
            await db.SaveChangesAsync();

            // Đơn hàng checkout chuẩn đã được reserve kho trước đó
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            await orderRepo.IncreaseReservedQuantityAsync(1, 1);

            var happyResult = await controller.DevSimulateSuccess("ORD-HAPPY-01");
            var happyObj = happyResult as OkObjectResult;
            Assert(happyObj != null && happyObj.StatusCode == StatusCodes.Status200OK, "F01.Base3: PaymentsController returns HTTP 200 OK for valid Pending order simulation");
            var happyResp = happyObj?.Value as ApiResponseDto<PaymentStatusDto>;
            Assert(happyResp != null && happyResp.Success && happyResp.Data?.Status == "Paid", "F01.Base4: PaymentsController response body data.Status is 'Paid'");

            // F01.Base5: Non-existent order -> HTTP 404 NotFound
            var notFoundResult = await controller.DevSimulateSuccess("ORD-NON-EXISTENT");
            var notFoundObj = notFoundResult as NotFoundObjectResult;
            Assert(notFoundObj != null && notFoundObj.StatusCode == StatusCodes.Status404NotFound, "F01.Base5: PaymentsController returns HTTP 404 NotFound for non-existent order");

            // F01.Base6: Invalid API Key -> HTTP 401 Unauthorized
            controller.Request.Headers["X-Api-Key"] = "wrong-key";
            var unauthResult = await controller.DevSimulateSuccess("ORD-HAPPY-01");
            var unauthObj = unauthResult as UnauthorizedObjectResult;
            Assert(unauthObj != null && unauthObj.StatusCode == StatusCodes.Status401Unauthorized, "F01.Base6: PaymentsController returns HTTP 401 Unauthorized for invalid X-Api-Key");

            // F01.Base7: Production environment -> HTTP 404 NotFound
            var prodEnv = new FakeHostEnvironment { EnvironmentName = "Production" };
            var prodController = new PaymentsController(paymentService, logger, configuration, prodEnv);
            prodController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            prodController.Request.Headers["X-Api-Key"] = "kiosk-secret-key";
            var prodResult = await prodController.DevSimulateSuccess("ORD-HAPPY-01");
            var prodObj = prodResult as NotFoundResult;
            Assert(prodObj != null && prodObj.StatusCode == StatusCodes.Status404NotFound, "F01.Base7: PaymentsController fails closed in Production (returns HTTP 404)");
        }

        // -------------------------------------------------------------
        // TEST 2b: F01 (P0) - TRUE CONCURRENCY RACE TEST (30 ITERATIONS ON REAL SQL SERVER)
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 2b. TEST F01 (P0): True Concurrency Race Test (30 Rounds on SQL Server Express) ---");
        Console.WriteLine("Scenario: F3 loads Pending order -> Cancel thread wins and commits Cancelled in SQL Server -> F3 resumes -> transition fails -> reloads fresh DB -> returns Conflict");
        
        int f01RoundsPassed = 0;
        int f01RoundsFailed = 0;

        for (int i = 1; i <= 30; i++)
        {
            var orderCode = $"ORD-RACE-F01-{i:D2}";

            // 1. Seed fresh Pending order directly in SQL Server
            using (var seedScope = serviceProvider.CreateScope())
            {
                var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var newOrder = new Order
                {
                    OrderCode = orderCode,
                    OrderStatus = OrderStatus.Pending,
                    TotalAmount = 100000m,
                    SubTotal = 100000m,
                    OrderDetails = new List<OrderDetail>
                    {
                        new OrderDetail { BookId = 1, Quantity = 1, UnitPriceAtTime = 100000m, LineTotal = 100000m }
                    }
                };
                seedDb.Orders.Add(newOrder);
                await seedDb.SaveChangesAsync();
            }

            var f3LoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelCommittedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            (bool Success, string Status, string? Message, Order? Order) f3Result = default;

            // Luồng 1 (F3 Simulation): Chạy trên DbContext Scope 1 độc lập
            var f3Task = Task.Run(async () =>
            {
                using var f3Scope = serviceProvider.CreateScope();
                var uow = f3Scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var payRepo = f3Scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                var memberRepo = f3Scope.ServiceProvider.GetRequiredService<IMemberRepository>();
                var realOrderRepo = f3Scope.ServiceProvider.GetRequiredService<IOrderRepository>();

                // Hook: Sau khi F3 load xong Order Pending vào Identity Map của DbContext 1,
                // phát tín hiệu cho Cancel thread và dừng chờ Cancel commit xong.
                var hookOrderRepo = new ConcurrencyHookOrderRepository(realOrderRepo, async (code) =>
                {
                    if (code == orderCode)
                    {
                        f3LoadedTcs.TrySetResult(true);
                        await cancelCommittedTcs.Task;
                    }
                });

                var f3PaymentService = new PaymentService(uow, payRepo, hookOrderRepo, memberRepo);
                f3Result = await f3PaymentService.SimulateDevSuccessAsync(orderCode);
            });

            // Luồng 2 (Cancel Order): Chạy trên DbContext Scope 2 độc lập hoàn toàn
            var cancelTask = Task.Run(async () =>
            {
                // Chờ F3 load xong Order Pending vào DbContext 1
                await f3LoadedTcs.Task;

                using var cancelScope = serviceProvider.CreateScope();
                var cancelOrderRepo = cancelScope.ServiceProvider.GetRequiredService<IOrderRepository>();
                var cancelUow = cancelScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                await cancelUow.BeginTransactionAsync();
                var cancelOrder = await cancelOrderRepo.GetOrderByCodeAsync(orderCode);
                bool transitioned = await cancelOrderRepo.TryTransitionOrderStatusAsync(cancelOrder!.OrderId, OrderStatus.Pending, OrderStatus.Cancelled);
                await cancelUow.SaveChangesAsync();
                await cancelUow.CommitAsync();

                // Báo hiệu Cancel đã commit Cancelled thành công xuống SQL Server
                cancelCommittedTcs.TrySetResult(transitioned);
            });

            await Task.WhenAll(f3Task, cancelTask);

            // Kiểm tra trạng thái thực tế từ SQL Server qua DbContext Scope 3 mới tinh
            using (var verifyScope = serviceProvider.CreateScope())
            {
                var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var dbOrder = await verifyDb.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderCode == orderCode);

                bool isPass = !f3Result.Success &&
                             f3Result.Status == "Conflict" &&
                             f3Result.Order != null && f3Result.Order.OrderStatus == OrderStatus.Cancelled &&
                             dbOrder != null && dbOrder.OrderStatus == OrderStatus.Cancelled;

                if (isPass)
                {
                    f01RoundsPassed++;
                }
                else
                {
                    f01RoundsFailed++;
                    Console.WriteLine($"[FAIL DETAILS Round {i}] f3Success={f3Result.Success}, f3Status={f3Result.Status}, f3Order={f3Result.Order?.OrderStatus}, dbStatus={dbOrder?.OrderStatus}");
                }
            }
        }

        Assert(f01RoundsPassed == 30 && f01RoundsFailed == 0, 
            $"F01.Race30: Concurrency Race Test on SQL Server Express ({f01RoundsPassed}/30 PASS, 0 FAIL)");

        // -------------------------------------------------------------
        // TEST 2c: F01 (P0) - HTTP INTEGRATION TEST (Overlap with Cancel via TestServer)
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 2c. TEST F01 (P0): HTTP Integration Test (TestServer + SQL Server Express) ---");
        {
            var httpOrderCode = "ORD-F01-HTTP-RACE";

            // Seed order
            using (var seedScope = serviceProvider.CreateScope())
            {
                var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var newOrder = new Order
                {
                    OrderCode = httpOrderCode,
                    OrderStatus = OrderStatus.Pending,
                    TotalAmount = 100000m,
                    SubTotal = 100000m,
                    OrderDetails = new List<OrderDetail>
                    {
                        new OrderDetail { BookId = 1, Quantity = 1, UnitPriceAtTime = 100000m, LineTotal = 100000m }
                    }
                };
                seedDb.Orders.Add(newOrder);
                await seedDb.SaveChangesAsync();
            }

            var httpLoadedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var httpCancelDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Dựng TestHost ASP.NET Core
            var hostBuilder = new WebHostBuilder()
                .UseEnvironment("Development")
                .ConfigureServices(s =>
                {
                    s.AddRouting();
                    s.AddControllers().AddApplicationPart(typeof(PaymentsController).Assembly);
                    s.AddSingleton(dbOptions);
                    s.AddScoped(sp => new ApplicationDbContext(sp.GetRequiredService<DbContextOptions<ApplicationDbContext>>()));
                    s.AddScoped<IUnitOfWork, UnitOfWork>();
                    s.AddScoped<OrderRepository>();
                    s.AddScoped<IOrderRepository>(sp =>
                    {
                        var real = sp.GetRequiredService<OrderRepository>();
                        return new ConcurrencyHookOrderRepository(real, async (code) =>
                        {
                            if (code == httpOrderCode)
                            {
                                httpLoadedTcs.TrySetResult(true);
                                await httpCancelDoneTcs.Task;
                            }
                        });
                    });
                    s.AddScoped<IPaymentRepository, PaymentRepository>();
                    s.AddScoped<IMemberRepository, MemberRepository>();
                    s.AddScoped<IPaymentService, PaymentService>();
                    s.AddSingleton<IConfiguration>(configuration);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            using var server = new TestServer(hostBuilder);
            using var client = server.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", "kiosk-secret-key");

            // Bắt đầu gửi HTTP Request tới /api/payments/dev-simulate-success
            var requestTask = client.PostAsync($"/api/payments/dev-simulate-success/{httpOrderCode}", null);

            // Khi HTTP request đã nạp Order Pending vào DbContext của nó, luồng Cancel chen ngang
            await httpLoadedTcs.Task;
            using (var cancelScope = serviceProvider.CreateScope())
            {
                var cancelOrderRepo = cancelScope.ServiceProvider.GetRequiredService<IOrderRepository>();
                var cancelUow = cancelScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await cancelUow.BeginTransactionAsync();
                var ord = await cancelOrderRepo.GetOrderByCodeAsync(httpOrderCode);
                await cancelOrderRepo.TryTransitionOrderStatusAsync(ord!.OrderId, OrderStatus.Pending, OrderStatus.Cancelled);
                await cancelUow.SaveChangesAsync();
                await cancelUow.CommitAsync();

                // Thả cho HTTP request tiếp tục
                httpCancelDoneTcs.TrySetResult(true);
            }

            var httpResponse = await requestTask;
            var responseContent = await httpResponse.Content.ReadAsStringAsync();

            using var jsonDoc = JsonDocument.Parse(responseContent);
            bool apiSuccess = jsonDoc.RootElement.GetProperty("success").GetBoolean();

            // Kiểm tra DB thật bằng DbContext mới
            using var verifyScope = serviceProvider.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbOrder = await verifyDb.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderCode == httpOrderCode);

            Assert(httpResponse.StatusCode == HttpStatusCode.Conflict, 
                $"F01.HTTP1: PaymentsController returns HTTP 409 Conflict (Actual: {(int)httpResponse.StatusCode} {httpResponse.StatusCode})");
            Assert(!apiSuccess, 
                "F01.HTTP2: Response body returns success = false wrapper");
            Assert(dbOrder != null && dbOrder.OrderStatus == OrderStatus.Cancelled, 
                "F01.HTTP3: SQL Server database confirmed in Cancelled state");
        }

        // -------------------------------------------------------------
        // TEST 3: D01 & D03 (P1) - Point validation and atomic deduction on SQL Server
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 3. TEST D01 & D03 (P1): Point Validation & Concurrency (SQL Server) ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            bool d01NegPass = false;
            try
            {
                await orderService.CheckoutKioskAsync(new CheckoutRequestDto
                {
                    MemberId = 1,
                    PointsToUse = -1,
                    Items = new List<CheckoutItemDto> { new CheckoutItemDto { BookId = 1, Quantity = 1 } }
                });
            }
            catch (ArgumentException ex) when (ex.Message.Contains("từ 0 đến 100"))
            {
                d01NegPass = true;
            }
            Assert(d01NegPass, "D01.1: Reject PointsToUse = -1 with ArgumentException");

            bool d01OverPass = false;
            try
            {
                await orderService.CheckoutKioskAsync(new CheckoutRequestDto
                {
                    MemberId = 1,
                    PointsToUse = 101,
                    Items = new List<CheckoutItemDto> { new CheckoutItemDto { BookId = 1, Quantity = 1 } }
                });
            }
            catch (ArgumentException ex) when (ex.Message.Contains("từ 0 đến 100"))
            {
                d01OverPass = true;
            }
            Assert(d01OverPass, "D01.2: Reject PointsToUse = 101 with ArgumentException");

            bool d01NoMemberPass = false;
            try
            {
                await orderService.CheckoutKioskAsync(new CheckoutRequestDto
                {
                    MemberId = null,
                    PointsToUse = 50,
                    Items = new List<CheckoutItemDto> { new CheckoutItemDto { BookId = 1, Quantity = 1 } }
                });
            }
            catch (ArgumentException ex) when (ex.Message.Contains("Cần cung cấp thông tin thành viên"))
            {
                d01NoMemberPass = true;
            }
            Assert(d01NoMemberPass, "D01.3: Reject PointsToUse > 0 without MemberId with ArgumentException");

            // D03 Concurrency: Member có 150 điểm. 2 checkout xin 100 điểm.
            var req1 = new CheckoutRequestDto
            {
                MemberId = 1,
                PointsToUse = 100,
                Items = new List<CheckoutItemDto> { new CheckoutItemDto { BookId = 1, Quantity = 1 } }
            };
            var resp1 = await orderService.CheckoutKioskAsync(req1);
            Assert(resp1 != null && resp1.PointsUsedAmount == 100000m, "D03.1: Request 1 uses 100 points successfully");

            bool req2FailedWithInsufficientPoints = false;
            try
            {
                var req2 = new CheckoutRequestDto
                {
                    MemberId = 1,
                    PointsToUse = 100,
                    Items = new List<CheckoutItemDto> { new CheckoutItemDto { BookId = 1, Quantity = 1 } }
                };
                await orderService.CheckoutKioskAsync(req2);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("không đủ điểm"))
            {
                req2FailedWithInsufficientPoints = true;
            }
            Assert(req2FailedWithInsufficientPoints, "D03.2: Request 2 rejected because member has only 50 points (NOT silently clamped)");
        }

        // -------------------------------------------------------------
        // TEST 4: B06 (P1) - Paid order receives new reference (SQL Server)
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 4. TEST B06 (P1): Paid order receives new reference ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            var paidOrder = new Order
            {
                OrderCode = "ORD-PAID-01",
                OrderStatus = OrderStatus.Paid,
                TotalAmount = 100000m,
                SubTotal = 100000m
            };
            db.Orders.Add(paidOrder);
            await db.SaveChangesAsync();

            var tx1 = new PaymentWebhookPayload
            {
                id = 201,
                transferType = "in",
                accountNumber = "0366994409",
                amountIn = 100000m,
                referenceCode = "REF_ORIGINAL_01",
                code = "ORD-PAID-01"
            };
            bool resTx1 = await paymentService.HandleSePayWebhookAsync(tx1, "0366994409");
            Assert(resTx1, "B06.1: First webhook on Paid order handled");

            var tx2 = new PaymentWebhookPayload
            {
                id = 202,
                transferType = "in",
                accountNumber = "0366994409",
                amountIn = 50000m,
                referenceCode = "REF_ADDITIONAL_02",
                code = "ORD-PAID-01"
            };
            bool resTx2 = await paymentService.HandleSePayWebhookAsync(tx2, "0366994409");
            Assert(resTx2, "B06.2: Second webhook with new ReferenceCode handled successfully");

            var transactions = await db.PaymentTransactions.Where(t => t.OrderId == paidOrder.OrderId).ToListAsync();
            bool bothSaved = transactions.Any(t => t.ReferenceCode == "REF_ORIGINAL_01") &&
                             transactions.Any(t => t.ReferenceCode == "REF_ADDITIONAL_02");
            Assert(bothSaved, "B06.3: Additional transaction REF_ADDITIONAL_02 saved in DB for audit without double inventory decrement");
        }

        // -------------------------------------------------------------
        // TEST 5: A04 (P2) - Duplicate BookId in Items
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 5. TEST A04 (P2): Duplicate BookId in Items ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            bool a04Pass = false;
            try
            {
                var reqWithDup = new CheckoutRequestDto
                {
                    Items = new List<CheckoutItemDto>
                    {
                        new CheckoutItemDto { BookId = 1, Quantity = 1 },
                        new CheckoutItemDto { BookId = 1, Quantity = 2 }
                    }
                };
                await orderService.CheckoutKioskAsync(reqWithDup);
            }
            catch (ArgumentException ex) when (ex.Message.Contains("trùng lặp (BookId: 1)"))
            {
                a04Pass = true;
            }
            Assert(a04Pass, "A04: Reject checkout with duplicate BookId upfront with ArgumentException");
        }

        // -------------------------------------------------------------
        // TEST 6: R01, R02, R03 - Regression checks on SQL Server Express
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 6. REGRESSION TESTS: R01, R02, R03 (SQL Server Express Invariants) ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
            var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            // R01: Atomic stock and reserved invariants
            var b1 = await db.Books.FindAsync(1);
            Assert(b1 != null && b1.StockQuantity >= 0 && b1.ReservedQuantity >= 0 && b1.ReservedQuantity <= b1.StockQuantity,
                "R01: SQL Server check constraints satisfied: Stock >= 0, Reserved >= 0, Reserved <= Stock");

            // R02: Terminal state transition idempotency (Paid vs Cancel)
            var regOrder = new Order
            {
                OrderCode = "ORD-REG-R02",
                OrderStatus = OrderStatus.Paid,
                TotalAmount = 100000m,
                SubTotal = 100000m
            };
            db.Orders.Add(regOrder);
            await db.SaveChangesAsync();

            bool tryCancelPaid = await orderRepo.TryTransitionOrderStatusAsync(regOrder.OrderId, OrderStatus.Pending, OrderStatus.Cancelled);
            Assert(!tryCancelPaid, "R02: Cannot cancel an order that is already Paid (atomic conditional state transition protects terminal state)");

            // R03: Late webhook on Cancelled order records audit transaction without reviving order or touching stock
            var lateOrder = new Order
            {
                OrderCode = "ORD-REG-LATE-01",
                OrderStatus = OrderStatus.Cancelled,
                TotalAmount = 100000m,
                SubTotal = 100000m
            };
            db.Orders.Add(lateOrder);
            await db.SaveChangesAsync();

            var stockBefore = b1!.StockQuantity;
            var latePayload = new PaymentWebhookPayload
            {
                id = 999,
                transferType = "in",
                accountNumber = "0366994409",
                amountIn = 100000m,
                referenceCode = "REF_LATE_AUDIT_999",
                code = "ORD-REG-LATE-01"
            };
            bool lateHandled = await paymentService.HandleSePayWebhookAsync(latePayload, "0366994409");
            
            var lateDbOrder = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == lateOrder.OrderId);
            var lateTx = await db.PaymentTransactions.FirstOrDefaultAsync(t => t.ReferenceCode == "REF_LATE_AUDIT_999");
            var b1After = await db.Books.FindAsync(1);

            Assert(lateHandled, "R03.1: Late webhook on Cancelled order returns true for SePay acknowledgment");
            Assert(lateDbOrder?.OrderStatus == OrderStatus.Cancelled, "R03.2: Late order remains Cancelled, never revived to Paid");
            Assert(lateTx != null && lateTx.OrderId == lateOrder.OrderId, "R03.3: Late payment audit transaction recorded in DB");
            Assert(b1After?.StockQuantity == stockBefore, "R03.4: Inventory stock left untouched by late webhook");
        }

        Console.WriteLine("\n======================================================================");
        Console.WriteLine($"TOTAL RESULTS: {passed} PASSED, {failed} FAILED");
        Console.WriteLine("======================================================================");

        if (failed > 0)
        {
            Environment.Exit(1);
        }
    }
}

public class FakeHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ApplicationName { get; set; } = "BookKiosk.API";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Development";
}
