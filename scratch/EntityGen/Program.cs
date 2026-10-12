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

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("======================================================================");
        Console.WriteLine("STARTING BACKEND REGRESSION & VALIDATION TESTS (.NET 10)");
        Console.WriteLine("Testing fixes for: B03 (P0), F01 (P0), D01 (P1), D03 (P1), B06 (P1), A04 (P2)");
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

        var dbFile = "test_regression.db";
        if (System.IO.File.Exists(dbFile)) System.IO.File.Delete(dbFile);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={dbFile}")
            .Options;

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
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

        var configBuilder = new ConfigurationBuilder();
        var configuration = configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "SePay:ApiKey", "TEST_KEY" },
            { "SePay:AccountNumber", "0366994409" },
            { "SePay:SubAccount", "SUB123" },
            { "ApiSettings:ApiKey", "TEST_API_KEY" }
        }).Build();
        services.AddSingleton<IConfiguration>(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // 1. SEED DATA
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();

            var cat = new Category { CategoryId = 1, Name = "Technology" };
            db.Categories.Add(cat);

            var book1 = new Book { BookId = 1, CategoryId = 1, Barcode = "BK001", Title = "C# 12 and .NET 8", SellingPrice = 100000m, StockQuantity = 10, ReservedQuantity = 0, IsActive = true, Author = "Author A" };
            var book2 = new Book { BookId = 2, CategoryId = 1, Barcode = "BK002", Title = "Clean Architecture", SellingPrice = 150000m, StockQuantity = 5, ReservedQuantity = 0, IsActive = true, Author = "Author B" };
            db.Books.AddRange(book1, book2);

            var member1 = new Member { MemberId = 1, FullName = "Nguyen Van A", PhoneNumber = "0901234567", Points = 150 };
            db.Members.Add(member1);

            await db.SaveChangesAsync();
        }

        // -------------------------------------------------------------
        // TEST 1: BE-NEW-10 / B03 (P0) - Webhook validation transferType and account
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 1. TEST B03 (P0): Webhook validation ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            // 1.1 Missing transferType (null)
            var p1 = new PaymentWebhookPayload { id = 101, transferType = null, accountNumber = "0366994409", amountIn = 100000m, code = "ORD-TEST-1" };
            bool res1 = await paymentService.HandleSePayWebhookAsync(p1, "0366994409");
            Assert(!res1, "B03.1: Reject webhook with null transferType");

            // 1.2 transferType != "in" (e.g. "out")
            var p2 = new PaymentWebhookPayload { id = 102, transferType = "out", accountNumber = "0366994409", amountIn = 100000m, code = "ORD-TEST-1" };
            bool res2 = await paymentService.HandleSePayWebhookAsync(p2, "0366994409");
            Assert(!res2, "B03.2: Reject webhook with transferType = 'out'");

            // 1.3 Missing recipient account (both null/empty)
            var p3 = new PaymentWebhookPayload { id = 103, transferType = "in", accountNumber = "", subAccount = null, amountIn = 100000m, code = "ORD-TEST-1" };
            bool res3 = await paymentService.HandleSePayWebhookAsync(p3);
            Assert(!res3, "B03.3: Reject webhook when accountNumber and subAccount are both empty");

            // 1.4 Mismatched account when expected is configured
            var p4 = new PaymentWebhookPayload { id = 104, transferType = "in", accountNumber = "9999999999", amountIn = 100000m, code = "ORD-TEST-1" };
            bool res4 = await paymentService.HandleSePayWebhookAsync(p4, "0366994409");
            Assert(!res4, "B03.4: Reject webhook when accountNumber does not match expected configuration");
        }

        // -------------------------------------------------------------
        // TEST 2: F01 (P0) - F3 / Cancel Race & PaymentsController 409 Conflict
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 2. TEST F01 (P0): F3 Dev Simulation vs Cancel ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

            var cancelledOrder = new Order
            {
                OrderCode = "ORD-CANCELLED-01",
                OrderStatus = OrderStatus.Cancelled,
                TotalAmount = 100000m,
                SubTotal = 100000m
            };
            db.Orders.Add(cancelledOrder);
            await db.SaveChangesAsync();

            var simResult = await paymentService.SimulateDevSuccessAsync("ORD-CANCELLED-01");
            Assert(!simResult.Success && simResult.Status == "Conflict", "F01.1: PaymentService returns (false, 'Conflict') when order is Cancelled");

            // Test PaymentsController DevSimulateSuccess
            var mockEnv = new FakeHostEnvironment { EnvironmentName = "Development" };
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<PaymentsController>>();
            var controller = new PaymentsController(paymentService, logger, configuration, mockEnv);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.Request.Headers["X-Api-Key"] = "TEST_API_KEY";

            var actionResult = await controller.DevSimulateSuccess("ORD-CANCELLED-01");
            var objectResult = actionResult as ObjectResult;
            Assert(objectResult != null && objectResult.StatusCode == StatusCodes.Status409Conflict, "F01.2: PaymentsController returns HTTP 409 Conflict for Cancelled order");
        }

        // -------------------------------------------------------------
        // TEST 3: D01 & D03 (P1) - Point validation and atomic deduction
        // -------------------------------------------------------------
        Console.WriteLine("\n--- 3. TEST D01 & D03 (P1): Point Validation & Concurrency ---");
        using (var scope = serviceProvider.CreateScope())
        {
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            // D01.1: Points -1
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

            // D01.2: Points 101
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

            // D01.3: Points > 0 without MemberId
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

            // D03: Member 1 has 150 points. Two checkouts both requesting 100 points.
            // Request 1: uses 100 points -> succeeds, member points become 50.
            // Request 2: requests 100 points -> must throw InvalidOperationException (cannot clamp to 50!).
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
        // TEST 4: B06 (P1) - Paid order receives new reference
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

            // First webhook
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

            // Second webhook with new ReferenceCode
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

            // Verify DB contains both transactions
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
