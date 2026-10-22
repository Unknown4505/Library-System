extern alias ApiAssembly;

using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Interfaces.Services;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using BookKiosk.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

// Aliases from ApiAssembly
using ApiKioskController = ApiAssembly::BookKiosk.API.Controllers.KioskController;
using ApiKeyMiddleware = ApiAssembly::BookKiosk.API.Middleware.ApiKeyMiddleware;

namespace BookKiosk.Tests.Kiosk;

public class KioskAdvertisingTests
{
    private ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    #region 1. SERVICE LAYER TESTS (Moq Repository & Fault-Isolation)

    [Fact]
    public async Task ADS_API_01_GetAdvertising_AllSourcesSucceed_ReturnsSuccessWithFullData()
    {
        // Arrange
        var repoMock = new Mock<IKioskAdvertisingRepository>();
        var promo = new AdvertisingPromotionDto { PromotionId = 1, Name = "Giảm 10k", PromotionType = PromotionType.OrderDiscount };
        var arrival = new AdvertisingNewArrivalDto { BookId = 10, Title = "Sách Mới", SellingPrice = 50000, AvailableStock = 5, LastImportedAt = DateTime.UtcNow };
        var best = new AdvertisingBestSellerDto { BookId = 20, Title = "Sách Bán Chạy", SellingPrice = 80000, AvailableStock = 3, TotalSold = 15 };

        repoMock.Setup(r => r.GetActivePromotionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<AdvertisingPromotionDto> { promo });
        repoMock.Setup(r => r.GetNewArrivalsAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync(new List<AdvertisingNewArrivalDto> { arrival });
        repoMock.Setup(r => r.GetBestSellersAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync(new List<AdvertisingBestSellerDto> { best });

        var service = new KioskAdvertisingService(repoMock.Object);

        // Act
        var result = await service.GetAdvertisingAsync();

        // Assert
        result.Status.Should().Be(AdvertisingStatus.Success);
        result.Message.Should().Be("Lấy dữ liệu quảng cáo thành công.");
        result.Data.Should().NotBeNull();
        result.Data.ActivePromotions.Should().HaveCount(1);
        result.Data.NewArrivals.Should().HaveCount(1);
        result.Data.BestSellers.Should().HaveCount(1);
        result.Data.UnavailableSections.Should().BeEmpty();
    }

    [Fact]
    public async Task ADS_API_08_GetAdvertising_AllSourcesEmpty_ReturnsSuccessWithEmptyMessage()
    {
        // Arrange
        var repoMock = new Mock<IKioskAdvertisingRepository>();
        repoMock.Setup(r => r.GetActivePromotionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<AdvertisingPromotionDto>());
        repoMock.Setup(r => r.GetNewArrivalsAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync(new List<AdvertisingNewArrivalDto>());
        repoMock.Setup(r => r.GetBestSellersAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync(new List<AdvertisingBestSellerDto>());

        var service = new KioskAdvertisingService(repoMock.Object);

        // Act
        var result = await service.GetAdvertisingAsync();

        // Assert
        result.Status.Should().Be(AdvertisingStatus.Success);
        result.Message.Should().Be("Hiện chưa có nội dung quảng cáo phù hợp.");
        result.Data.Should().NotBeNull();
        result.Data.ActivePromotions.Should().BeEmpty();
        result.Data.NewArrivals.Should().BeEmpty();
        result.Data.BestSellers.Should().BeEmpty();
        result.Data.UnavailableSections.Should().BeEmpty();
    }

    [Fact]
    public async Task ADS_API_09_GetAdvertising_OneSourceThrows_ReturnsPartialSuccessWithUnavailableSection()
    {
        // Arrange
        var repoMock = new Mock<IKioskAdvertisingRepository>();
        var promo = new AdvertisingPromotionDto { PromotionId = 1, Name = "Giảm 10k" };
        var arrival = new AdvertisingNewArrivalDto { BookId = 10, Title = "Sách Mới" };

        repoMock.Setup(r => r.GetActivePromotionsAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<AdvertisingPromotionDto> { promo });
        repoMock.Setup(r => r.GetNewArrivalsAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync(new List<AdvertisingNewArrivalDto> { arrival });
        // bestSellers throws DB Exception
        repoMock.Setup(r => r.GetBestSellersAsync(It.IsAny<DateTime>(), 10))
            .ThrowsAsync(new TimeoutException("Database query timeout"));

        var service = new KioskAdvertisingService(repoMock.Object);

        // Act
        var result = await service.GetAdvertisingAsync();

        // Assert
        result.Status.Should().Be(AdvertisingStatus.PartialSuccess);
        result.Message.Should().Be("Một số nội dung quảng cáo tạm thời không khả dụng.");
        result.Data.Should().NotBeNull();
        result.Data.ActivePromotions.Should().HaveCount(1);
        result.Data.NewArrivals.Should().HaveCount(1);
        result.Data.BestSellers.Should().BeEmpty();
        result.Data.UnavailableSections.Should().ContainSingle().Which.Should().Be("bestSellers");
    }

    [Fact]
    public async Task ADS_API_09B_GetAdvertising_TwoSourcesThrow_ReturnsPartialSuccess()
    {
        // Arrange
        var repoMock = new Mock<IKioskAdvertisingRepository>();
        var best = new AdvertisingBestSellerDto { BookId = 20, Title = "Sách Bán Chạy" };

        repoMock.Setup(r => r.GetActivePromotionsAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("Promo DB error"));
        repoMock.Setup(r => r.GetNewArrivalsAsync(It.IsAny<DateTime>(), 10))
            .ThrowsAsync(new Exception("Arrivals DB error"));
        repoMock.Setup(r => r.GetBestSellersAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync(new List<AdvertisingBestSellerDto> { best });

        var service = new KioskAdvertisingService(repoMock.Object);

        // Act
        var result = await service.GetAdvertisingAsync();

        // Assert
        result.Status.Should().Be(AdvertisingStatus.PartialSuccess);
        result.Data.UnavailableSections.Should().BeEquivalentTo(new[] { "activePromotions", "newArrivals" });
        result.Data.BestSellers.Should().HaveCount(1);
    }

    [Fact]
    public async Task ADS_API_10_GetAdvertising_AllThreeSourcesThrow_ReturnsServiceUnavailable()
    {
        // Arrange
        var repoMock = new Mock<IKioskAdvertisingRepository>();
        repoMock.Setup(r => r.GetActivePromotionsAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("DB Down 1"));
        repoMock.Setup(r => r.GetNewArrivalsAsync(It.IsAny<DateTime>(), 10))
            .ThrowsAsync(new Exception("DB Down 2"));
        repoMock.Setup(r => r.GetBestSellersAsync(It.IsAny<DateTime>(), 10))
            .ThrowsAsync(new Exception("DB Down 3"));

        var service = new KioskAdvertisingService(repoMock.Object);

        // Act
        var result = await service.GetAdvertisingAsync();

        // Assert
        result.Status.Should().Be(AdvertisingStatus.ServiceUnavailable);
        result.Message.Should().Be("Nội dung quảng cáo tạm thời không khả dụng.");
        result.Data.Should().BeNull();
    }

    #endregion

    #region 2. REPOSITORY QUERIES & BUSINESS RULES TESTS (ActivePromotions, NewArrivals, BestSellers)

    [Fact]
    public async Task ADS_API_03_GetActivePromotions_ValidOrderDiscount_ReturnsOrderDiscountWithUtcZ()
    {
        // Arrange
        using var context = CreateInMemoryDbContext("Test_Promo_OrderDiscount");
        var now = DateTime.UtcNow;

        var promo = new Promotion
        {
            PromotionId = 1,
            Name = "KM Hợp lệ",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-2),
            EndDate = now.AddDays(5),
            OrderDiscount = new PromotionOrderDiscount
            {
                PromotionId = 1,
                MinOrderValue = 100000,
                DiscountAmount = 15000
            }
        };
        context.Promotions.Add(promo);
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetActivePromotionsAsync(now);

        // Assert
        results.Should().HaveCount(1);
        var item = results[0];
        item.PromotionId.Should().Be(1);
        item.PromotionType.Should().Be(PromotionType.OrderDiscount);
        item.OrderDiscount.Should().NotBeNull();
        item.OrderDiscount!.MinOrderValue.Should().Be(100000);
        item.OrderDiscount.DiscountAmount.Should().Be(15000);
        item.ProductDiscounts.Should().BeEmpty();
        item.StartDate.Kind.Should().Be(DateTimeKind.Utc);
        item.EndDate.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ADS_API_04_GetActivePromotions_ProductDiscount_IsFilteredOut()
    {
        // Arrange: ProductDiscount phải bị lọc khỏi Kiosk Advertising V1
        using var context = CreateInMemoryDbContext("Test_Promo_ProductDiscount");
        var now = DateTime.UtcNow;

        var promo = new Promotion
        {
            PromotionId = 2,
            Name = "KM ProductDiscount",
            PromotionType = PromotionType.ProductDiscount,
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(2)
        };
        context.Promotions.Add(promo);
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetActivePromotionsAsync(now);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ADS_API_05A_GetActivePromotions_ExactStartDateBoundary_IsIncluded()
    {
        // Arrange: StartDate == utcNow phải được bao gồm
        using var context = CreateInMemoryDbContext("Test_Promo_Boundary_Start");
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        var promoStartBoundary = new Promotion
        {
            PromotionId = 10,
            Name = "Start Boundary Match",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now, // Đúng bằng now
            EndDate = now.AddDays(1),
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        context.Promotions.Add(promoStartBoundary);
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetActivePromotionsAsync(now);

        // Assert
        results.Should().HaveCount(1);
        results[0].PromotionId.Should().Be(10);
    }

    [Fact]
    public async Task ADS_API_05B_GetActivePromotions_ExactEndDateBoundary_IsIncluded()
    {
        // Arrange: EndDate == utcNow phải được bao gồm
        using var context = CreateInMemoryDbContext("Test_Promo_Boundary_End");
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        var promoEndBoundary = new Promotion
        {
            PromotionId = 11,
            Name = "End Boundary Match",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now, // Đúng bằng now
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        context.Promotions.Add(promoEndBoundary);
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetActivePromotionsAsync(now);

        // Assert
        results.Should().HaveCount(1);
        results[0].PromotionId.Should().Be(11);
    }

    [Fact]
    public async Task ADS_API_05C_GetActivePromotions_OutsideBoundary_AreExcluded()
    {
        // Arrange: Trước StartDate 1s hoặc sau EndDate 1s bị loại
        using var context = CreateInMemoryDbContext("Test_Promo_OutsideBoundary");
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);

        var promoFuture = new Promotion
        {
            PromotionId = 20,
            Name = "Future Promo",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddSeconds(1), // Chưa bắt đầu 1s
            EndDate = now.AddDays(5),
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        var promoPast = new Promotion
        {
            PromotionId = 21,
            Name = "Past Promo",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-5),
            EndDate = now.AddSeconds(-1), // Đã hết hạn 1s
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        var promoInactive = new Promotion
        {
            PromotionId = 22,
            Name = "Inactive Promo",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = false, // Inactive
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        context.Promotions.AddRange(promoFuture, promoPast, promoInactive);
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetActivePromotionsAsync(now);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ADS_API_05D_GetActivePromotions_Sorting_OrdersByEndDateAscThenPromotionIdAsc()
    {
        // Arrange
        using var context = CreateInMemoryDbContext("Test_Promo_Sorting");
        var now = DateTime.UtcNow;

        var promo1 = new Promotion
        {
            PromotionId = 1,
            Name = "Hết hạn muộn hơn",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(10),
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        var promo2 = new Promotion
        {
            PromotionId = 2,
            Name = "Hết hạn sớm nhất",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(2),
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        var promo3 = new Promotion
        {
            PromotionId = 3,
            Name = "Cùng ngày hết hạn với 2 nhưng ID lớn hơn",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(2),
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };

        context.Promotions.AddRange(promo1, promo2, promo3);
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetActivePromotionsAsync(now);

        // Assert: Thứ tự mong muốn: 2 (sớm, id nhỏ) -> 3 (sớm, id lớn) -> 1 (muộn)
        results.Select(p => p.PromotionId).Should().ContainInOrder(2, 3, 1);
    }

    [Fact]
    public async Task ADS_API_05E_GetActivePromotions_OrderDiscountNullRecord_IsExcluded()
    {
        // Arrange: Có type OrderDiscount nhưng OrderDiscount == null
        using var context = CreateInMemoryDbContext("Test_Promo_NullDiscount");
        var now = DateTime.UtcNow;

        var promo = new Promotion
        {
            PromotionId = 30,
            Name = "Khuyết record OrderDiscount",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(5),
            OrderDiscount = null // Khuyết dữ liệu quan hệ
        };
        context.Promotions.Add(promo);
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetActivePromotionsAsync(now);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ADS_API_06_GetNewArrivals_Rolling30Days_OnlyActiveInStock_SortedAndLimit10()
    {
        // Arrange
        using var context = CreateInMemoryDbContext("Test_NewArrivals");
        var now = DateTime.UtcNow;

        var b1 = new Book { BookId = 1, Title = "Sách 1", StockQuantity = 10, ReservedQuantity = 2, IsActive = true, SellingPrice = 50000 };
        var b2 = new Book { BookId = 2, Title = "Sách 2", StockQuantity = 5, ReservedQuantity = 0, IsActive = true, SellingPrice = 70000 };
        var bOld = new Book { BookId = 3, Title = "Sách Cũ 40 Ngày", StockQuantity = 10, ReservedQuantity = 0, IsActive = true, SellingPrice = 60000 };
        var bOutStock = new Book { BookId = 4, Title = "Sách Hết Hàng", StockQuantity = 5, ReservedQuantity = 5, IsActive = true, SellingPrice = 80000 };
        var bInactive = new Book { BookId = 5, Title = "Sách Inactive", StockQuantity = 10, ReservedQuantity = 0, IsActive = false, SellingPrice = 90000 };

        context.Books.AddRange(b1, b2, bOld, bOutStock, bInactive);

        var rRecent1 = new ImportReceipt { ImportReceiptId = 1, CreatedAt = now.AddDays(-5) };
        var rRecent2 = new ImportReceipt { ImportReceiptId = 2, CreatedAt = now.AddDays(-2) }; // Mới hơn
        var rOld = new ImportReceipt { ImportReceiptId = 3, CreatedAt = now.AddDays(-35) }; // Quá 30 ngày

        context.ImportReceipts.AddRange(rRecent1, rRecent2, rOld);

        context.ImportReceiptDetails.AddRange(
            new ImportReceiptDetail { ImportReceiptId = 1, BookId = 1, Quantity = 5, CostPrice = 30000 },
            new ImportReceiptDetail { ImportReceiptId = 2, BookId = 2, Quantity = 5, CostPrice = 40000 },
            new ImportReceiptDetail { ImportReceiptId = 3, BookId = 3, Quantity = 5, CostPrice = 35000 },
            new ImportReceiptDetail { ImportReceiptId = 2, BookId = 4, Quantity = 5, CostPrice = 50000 },
            new ImportReceiptDetail { ImportReceiptId = 2, BookId = 5, Quantity = 5, CostPrice = 50000 }
        );
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetNewArrivalsAsync(now, 10);

        // Assert
        results.Should().HaveCount(2);
        // Sách 2 mới hơn (-2 ngày) phải đứng trước Sách 1 (-5 ngày)
        results[0].BookId.Should().Be(2);
        results[0].LastImportedAt.Kind.Should().Be(DateTimeKind.Utc);
        results[0].AvailableStock.Should().Be(5);

        results[1].BookId.Should().Be(1);
        results[1].AvailableStock.Should().Be(8); // 10 - 2
    }

    [Fact]
    public async Task ADS_API_07_GetBestSellers_PaidOrdersOnly_Rolling30Days_SortedAndLimit10()
    {
        // Arrange
        using var context = CreateInMemoryDbContext("Test_BestSellers");
        var now = DateTime.UtcNow;

        var b1 = new Book { BookId = 1, Title = "Clean Code", StockQuantity = 10, ReservedQuantity = 1, IsActive = true, SellingPrice = 100000 };
        var b2 = new Book { BookId = 2, Title = "Design Patterns", StockQuantity = 20, ReservedQuantity = 0, IsActive = true, SellingPrice = 120000 };
        var bOutOfStock = new Book { BookId = 3, Title = "Refactoring", StockQuantity = 2, ReservedQuantity = 2, IsActive = true, SellingPrice = 90000 };

        context.Books.AddRange(b1, b2, bOutOfStock);

        var oPaidRecent = new Order { OrderId = 1, OrderStatus = OrderStatus.Paid, CompletedAt = now.AddDays(-10) };
        var oPaidOld = new Order { OrderId = 2, OrderStatus = OrderStatus.Paid, CompletedAt = now.AddDays(-40) }; // Quá 30 ngày
        var oCancelled = new Order { OrderId = 3, OrderStatus = OrderStatus.Cancelled, CompletedAt = now.AddDays(-5) }; // Đơn hủy

        context.Orders.AddRange(oPaidRecent, oPaidOld, oCancelled);

        context.OrderDetails.AddRange(
            new OrderDetail { OrderId = 1, BookId = 1, Quantity = 3 },
            new OrderDetail { OrderId = 1, BookId = 2, Quantity = 8 },
            new OrderDetail { OrderId = 1, BookId = 3, Quantity = 15 }, // Hết hàng khả dụng -> bị loại
            new OrderDetail { OrderId = 2, BookId = 1, Quantity = 10 }, // Đơn cũ -> bị loại
            new OrderDetail { OrderId = 3, BookId = 1, Quantity = 20 }  // Đơn hủy -> bị loại
        );
        await context.SaveChangesAsync();

        var repo = new KioskAdvertisingRepository(context);

        // Act
        var results = await repo.GetBestSellersAsync(now, 10);

        // Assert: Chỉ lấy từ oPaidRecent trong 30 ngày cho sách còn hàng
        results.Should().HaveCount(2);
        // Sách 2 bán 8 cuốn xếp trước Sách 1 bán 3 cuốn
        results[0].BookId.Should().Be(2);
        results[0].TotalSold.Should().Be(8);

        results[1].BookId.Should().Be(1);
        results[1].TotalSold.Should().Be(3);
    }

    #endregion

    #region 3. CONTROLLER LAYER TESTS (HTTP Responses & Error Handling)

    [Fact]
    public async Task CTRL_01_Controller_GetAdvertising_Success_Returns200OkWithPayload()
    {
        // Arrange
        var kioskServiceMock = new Mock<IKioskService>();
        var adServiceMock = new Mock<IKioskAdvertisingService>();

        var expectedDto = new KioskAdvertisingDto();
        adServiceMock.Setup(s => s.GetAdvertisingAsync())
            .ReturnsAsync(new KioskAdvertisingResult
            {
                Status = AdvertisingStatus.Success,
                Message = "Lấy dữ liệu quảng cáo thành công.",
                Data = expectedDto
            });

        var controller = new ApiKioskController(kioskServiceMock.Object, adServiceMock.Object);

        // Act
        var actionResult = await controller.GetAdvertising();

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult;
        ok.StatusCode.Should().Be(200);

        var apiResp = ok.Value as ApiResponseDto<KioskAdvertisingDto>;
        apiResp.Should().NotBeNull();
        apiResp!.Success.Should().BeTrue();
        apiResp.Code.Should().Be("SUCCESS");
        apiResp.Message.Should().Be("Lấy dữ liệu quảng cáo thành công.");
    }

    [Fact]
    public async Task CTRL_02_Controller_GetAdvertising_PartialSuccess_Returns200OkWithPartialCode()
    {
        // Arrange
        var kioskServiceMock = new Mock<IKioskService>();
        var adServiceMock = new Mock<IKioskAdvertisingService>();

        var expectedDto = new KioskAdvertisingDto { UnavailableSections = new List<string> { "bestSellers" } };
        adServiceMock.Setup(s => s.GetAdvertisingAsync())
            .ReturnsAsync(new KioskAdvertisingResult
            {
                Status = AdvertisingStatus.PartialSuccess,
                Message = "Một số nội dung quảng cáo tạm thời không khả dụng.",
                Data = expectedDto
            });

        var controller = new ApiKioskController(kioskServiceMock.Object, adServiceMock.Object);

        // Act
        var actionResult = await controller.GetAdvertising();

        // Assert
        actionResult.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)actionResult;
        ok.StatusCode.Should().Be(200);

        var apiResp = ok.Value as ApiResponseDto<KioskAdvertisingDto>;
        apiResp.Should().NotBeNull();
        apiResp!.Success.Should().BeTrue();
        apiResp.Code.Should().Be("PARTIAL_SUCCESS");
        apiResp.Data!.UnavailableSections.Should().Contain("bestSellers");
    }

    [Fact]
    public async Task CTRL_03_Controller_GetAdvertising_ServiceUnavailable_Returns503StatusCode()
    {
        // Arrange
        var kioskServiceMock = new Mock<IKioskService>();
        var adServiceMock = new Mock<IKioskAdvertisingService>();

        adServiceMock.Setup(s => s.GetAdvertisingAsync())
            .ReturnsAsync(new KioskAdvertisingResult
            {
                Status = AdvertisingStatus.ServiceUnavailable,
                Message = "Nội dung quảng cáo tạm thời không khả dụng.",
                Data = null!
            });

        var controller = new ApiKioskController(kioskServiceMock.Object, adServiceMock.Object);

        // Act
        var actionResult = await controller.GetAdvertising();

        // Assert
        actionResult.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)actionResult;
        objResult.StatusCode.Should().Be(503);

        var apiResp = objResult.Value as ApiResponseDto<KioskAdvertisingDto>;
        apiResp.Should().NotBeNull();
        apiResp!.Success.Should().BeFalse();
        apiResp.Code.Should().Be("SERVICE_UNAVAILABLE");
        apiResp.Data.Should().BeNull();
    }

    [Fact]
    public async Task CTRL_04_Controller_GetAdvertising_UnexpectedException_Returns500InternalError()
    {
        // Arrange
        var kioskServiceMock = new Mock<IKioskService>();
        var adServiceMock = new Mock<IKioskAdvertisingService>();

        adServiceMock.Setup(s => s.GetAdvertisingAsync())
            .ThrowsAsync(new OutOfMemoryException("Fatal unhandled error"));

        var controller = new ApiKioskController(kioskServiceMock.Object, adServiceMock.Object);

        // Act
        var actionResult = await controller.GetAdvertising();

        // Assert
        actionResult.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)actionResult;
        objResult.StatusCode.Should().Be(500);

        var apiResp = objResult.Value as ApiResponseDto<object>;
        apiResp.Should().NotBeNull();
        apiResp!.Success.Should().BeFalse();
        apiResp.Code.Should().Be("INTERNAL_ERROR");
        apiResp.Message.Should().Be("Hệ thống gặp sự cố. Vui lòng thử lại sau.");
    }

    #endregion

    #region 4. MIDDLEWARE & AUTHENTICATION TESTS (ApiKeyMiddleware 401/403/Pass)

    [Fact]
    public async Task ADS_API_02A_ApiKeyMiddleware_MissingKey_Returns401Unauthorized()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/kiosk/advertising";
        var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ApiSettings:ApiKey", "valid-kiosk-secret" }
            })
            .Build();

        bool nextCalled = false;
        var middleware = new ApiKeyMiddleware((innerContext) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, config);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(memoryStream);
        var body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("code").GetString().Should().Be("UNAUTHORIZED");
        json.RootElement.GetProperty("message").GetString().Should().Be("Thiếu thông tin xác thực Kiosk.");
    }

    [Fact]
    public async Task ADS_API_02B_ApiKeyMiddleware_InvalidKey_Returns403Forbidden()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/kiosk/advertising";
        context.Request.Headers["X-API-KEY"] = "wrong-api-key";
        var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ApiSettings:ApiKey", "valid-kiosk-secret" }
            })
            .Build();

        bool nextCalled = false;
        var middleware = new ApiKeyMiddleware((innerContext) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, config);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(403);

        memoryStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(memoryStream);
        var body = await reader.ReadToEndAsync();
        var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        json.RootElement.GetProperty("code").GetString().Should().Be("FORBIDDEN");
        json.RootElement.GetProperty("message").GetString().Should().Be("Kiosk không có quyền truy cập chức năng này.");
    }

    [Fact]
    public async Task ADS_API_02C_ApiKeyMiddleware_ValidKey_InvokesNextDelegateSuccessfully()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/kiosk/advertising";
        context.Request.Headers["X-API-KEY"] = "valid-kiosk-secret";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ApiSettings:ApiKey", "valid-kiosk-secret" }
            })
            .Build();

        bool nextCalled = false;
        var middleware = new ApiKeyMiddleware((innerContext) =>
        {
            nextCalled = true;
            innerContext.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, config);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    #endregion

    #region 5. READ-ONLY & IDEMPOTENCY TESTS (ADS-API-12)

    [Fact]
    public async Task ADS_API_12_GetAdvertising_ReadOnly_TenConsecutiveCalls_NoSideEffectsAndConsistentData()
    {
        // Arrange
        using var context = CreateInMemoryDbContext("Test_ReadOnly_Idempotency");
        var now = DateTime.UtcNow;

        var promo = new Promotion
        {
            PromotionId = 1,
            Name = "KM 1",
            PromotionType = PromotionType.OrderDiscount,
            IsActive = true,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 50000, DiscountAmount = 5000 }
        };
        var book = new Book
        {
            BookId = 100,
            Title = "Kiosk Read-Only Book",
            SellingPrice = 50000,
            StockQuantity = 10,
            ReservedQuantity = 1,
            IsActive = true
        };
        var receipt = new ImportReceipt { ImportReceiptId = 10, CreatedAt = now.AddDays(-2) };
        var receiptDetail = new ImportReceiptDetail { ImportReceiptId = 10, BookId = 100, Quantity = 10, CostPrice = 30000 };
        var order = new Order { OrderId = 100, OrderStatus = OrderStatus.Paid, CompletedAt = now.AddDays(-1) };
        var orderDetail = new OrderDetail { OrderId = 100, BookId = 100, Quantity = 2 };

        context.Promotions.Add(promo);
        context.Books.Add(book);
        context.ImportReceipts.Add(receipt);
        context.ImportReceiptDetails.Add(receiptDetail);
        context.Orders.Add(order);
        context.OrderDetails.Add(orderDetail);
        await context.SaveChangesAsync();

        var initialPromotionsCount = await context.Promotions.CountAsync();
        var initialBooksCount = await context.Books.CountAsync();
        var initialOrdersCount = await context.Orders.CountAsync();
        var initialReceiptsCount = await context.ImportReceipts.CountAsync();

        var repo = new KioskAdvertisingRepository(context);
        var service = new KioskAdvertisingService(repo);

        // Act: Gọi 10 lần liên tiếp
        var results = new List<KioskAdvertisingResult>();
        for (int i = 0; i < 10; i++)
        {
            var res = await service.GetAdvertisingAsync();
            results.Add(res);
        }

        // Assert:
        // 1. Cả 10 lần đều trả SUCCESS
        results.Should().HaveCount(10);
        foreach (var r in results)
        {
            r.Status.Should().Be(AdvertisingStatus.Success);
            r.Data.Should().NotBeNull();
            r.Data!.ActivePromotions.Should().HaveCount(1);
            r.Data.NewArrivals.Should().HaveCount(1);
            r.Data.BestSellers.Should().HaveCount(1);
            r.Data.UnavailableSections.Should().BeEmpty();
        }

        // 2. Không có bất kỳ sự thay đổi dữ liệu nào trong DB (Read-Only)
        (await context.Promotions.CountAsync()).Should().Be(initialPromotionsCount);
        (await context.Books.CountAsync()).Should().Be(initialBooksCount);
        (await context.Orders.CountAsync()).Should().Be(initialOrdersCount);
        (await context.ImportReceipts.CountAsync()).Should().Be(initialReceiptsCount);

        // 3. Không có entity nào bị tracking ở trạng thái modified/added/deleted
        context.ChangeTracker.Entries().Any(e => e.State != EntityState.Unchanged).Should().BeFalse();
    }

    #endregion

    #region 6. REAL SQL SERVER RUNTIME SMOKE TEST (Outer Apply & Translation Verification)

    [Fact]
    public async Task ADS_SQL_SMOKE_RealSqlServer_QueriesExecuteSuccessfullyWithoutTranslationErrors()
    {
        // Disposable database name trên SQL Server Express / Developer localhost
        var disposableDbName = $"BookKiosk_Smoke_ADS_{Guid.NewGuid():N}";
        var testConnStr = $"Server=localhost;Database={disposableDbName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(testConnStr)
            .Options;

        using var context = new ApplicationDbContext(options);

        try
        {
            // 1. Tạo database thật trên SQL Server Express / Developer
            await context.Database.EnsureCreatedAsync();

            var now = DateTime.UtcNow;

            // 2. Seed dữ liệu thực tế
            var promo = new Promotion
            {
                Name = "SQL Smoke OrderDiscount Promo",
                PromotionType = PromotionType.OrderDiscount,
                IsActive = true,
                StartDate = now.AddDays(-2),
                EndDate = now.AddDays(5),
                OrderDiscount = new PromotionOrderDiscount
                {
                    MinOrderValue = 100000,
                    DiscountAmount = 10000
                }
            };
            var book = new Book
            {
                Title = "Real SQL Book",
                SellingPrice = 85000,
                StockQuantity = 20,
                ReservedQuantity = 2,
                IsActive = true
            };
            context.Promotions.Add(promo);
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var receipt = new ImportReceipt { CreatedAt = now.AddDays(-3) };
            context.ImportReceipts.Add(receipt);
            await context.SaveChangesAsync();

            context.ImportReceiptDetails.Add(new ImportReceiptDetail
            {
                ImportReceiptId = receipt.ImportReceiptId,
                BookId = book.BookId,
                Quantity = 10,
                CostPrice = 50000
            });

            var order = new Order
            {
                OrderStatus = OrderStatus.Paid,
                CompletedAt = now.AddDays(-1)
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            context.OrderDetails.Add(new OrderDetail
            {
                OrderId = order.OrderId,
                BookId = book.BookId,
                Quantity = 4
            });
            await context.SaveChangesAsync();

            // 3. Thực thi 3 câu query trên SQL Server thật
            var repo = new KioskAdvertisingRepository(context);

            var activePromos = await repo.GetActivePromotionsAsync(now);
            var newArrivals = await repo.GetNewArrivalsAsync(now, 10);
            var bestSellers = await repo.GetBestSellersAsync(now, 10);

            // 4. Assert SQL Server LINQ translation thành công mượt mà
            activePromos.Should().NotBeNull();
            activePromos.Should().HaveCount(1);
            activePromos[0].StartDate.Kind.Should().Be(DateTimeKind.Utc);
            activePromos[0].EndDate.Kind.Should().Be(DateTimeKind.Utc);

            newArrivals.Should().NotBeNull();
            newArrivals.Should().HaveCount(1);
            newArrivals[0].BookId.Should().Be(book.BookId);
            newArrivals[0].LastImportedAt.Kind.Should().Be(DateTimeKind.Utc);

            bestSellers.Should().NotBeNull();
            bestSellers.Should().HaveCount(1);
            bestSellers[0].BookId.Should().Be(book.BookId);
            bestSellers[0].TotalSold.Should().Be(4);
        }
        finally
        {
            // 5. Cleanup: Luôn xóa disposable database sau khi chạy xong
            try
            {
                await context.Database.EnsureDeletedAsync();
            }
            catch
            {
                // Bỏ qua lỗi dọn dẹp nếu DB chưa được tạo
            }
        }
    }

    #endregion
}
