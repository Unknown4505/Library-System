using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Services;
using BookKiosk.Domain.Entities;
using FluentAssertions;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace BookKiosk.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly Mock<IPromotionRepository> _mockPromotionRepository;
    private readonly Mock<IMemberRepository> _mockMemberRepository;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockOrderRepository = new Mock<IOrderRepository>();
        _mockPromotionRepository = new Mock<IPromotionRepository>();
        _mockMemberRepository = new Mock<IMemberRepository>();

        _orderService = new OrderService(
            _mockUnitOfWork.Object,
            _mockOrderRepository.Object,
            _mockPromotionRepository.Object,
            _mockMemberRepository.Object);
    }

    [Fact]
    public async Task CheckoutKioskAsync_ShouldCalculatePointsCorrectly_WhenPointsAreUsed()
    {
        // Arrange
        var request = new CheckoutRequestDto
        {
            MemberId = 1,
            PointsToUse = 50, // 50 points = 50,000 VND
            Items = new List<CheckoutItemDto>
            {
                new CheckoutItemDto { BookId = 1, Quantity = 2 }
            }
        };

        var bookDict = new Dictionary<int, Book>
        {
            { 1, new Book { BookId = 1, Title = "Book A", SellingPrice = 100000, StockQuantity = 10, ReservedQuantity = 0 } }
        };

        var member = new Member { MemberId = 1, Points = 100 };

        _mockOrderRepository.Setup(r => r.GetBooksByIdsAsync(It.IsAny<List<int>>())).ReturnsAsync(bookDict);
        _mockPromotionRepository.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(new List<Promotion>());
        _mockMemberRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(member);

        // Act
        var response = await _orderService.CheckoutKioskAsync(request);

        // Assert
        response.SubTotal.Should().Be(200000); // 2 * 100,000
        response.PointsUsedAmount.Should().Be(50000); // 50 * 1,000
        response.DiscountAmount.Should().Be(0);
        response.TotalAmount.Should().Be(150000); // 200,000 - 50,000
    }

    [Fact]
    public async Task CheckoutKioskAsync_ShouldApplyPromotionCorrectly_WhenOrderMeetsCriteria()
    {
        // Arrange
        var request = new CheckoutRequestDto
        {
            MemberId = null,
            PointsToUse = 0,
            Items = new List<CheckoutItemDto>
            {
                new CheckoutItemDto { BookId = 1, Quantity = 3 }
            }
        };

        var bookDict = new Dictionary<int, Book>
        {
            { 1, new Book { BookId = 1, Title = "Book A", SellingPrice = 100000, StockQuantity = 10, ReservedQuantity = 0 } }
        };

        // SubTotal = 300,000
        var promotions = new List<Promotion>
        {
            new Promotion 
            { 
                PromotionId = 1, 
                OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 200000, DiscountAmount = 20000 }
            },
            new Promotion 
            { 
                PromotionId = 2, 
                OrderDiscount = new PromotionOrderDiscount { MinOrderValue = 500000, DiscountAmount = 100000 } // Not met
            }
        };

        _mockOrderRepository.Setup(r => r.GetBooksByIdsAsync(It.IsAny<List<int>>())).ReturnsAsync(bookDict);
        _mockPromotionRepository.Setup(r => r.GetAllActiveAsync()).ReturnsAsync(promotions);

        // Act
        var response = await _orderService.CheckoutKioskAsync(request);

        // Assert
        response.SubTotal.Should().Be(300000); // 3 * 100,000
        response.DiscountAmount.Should().Be(20000);
        response.PointsUsedAmount.Should().Be(0);
        response.TotalAmount.Should().Be(280000); // 300,000 - 20,000
    }

    [Fact]
    public async Task CheckoutKioskAsync_ShouldThrowException_WhenStockIsInsufficient()
    {
        // Arrange
        var request = new CheckoutRequestDto
        {
            Items = new List<CheckoutItemDto>
            {
                new CheckoutItemDto { BookId = 1, Quantity = 5 }
            }
        };

        var bookDict = new Dictionary<int, Book>
        {
            // Only 3 available (5 stock - 2 reserved)
            { 1, new Book { BookId = 1, Title = "Book A", SellingPrice = 100000, StockQuantity = 5, ReservedQuantity = 2 } }
        };

        _mockOrderRepository.Setup(r => r.GetBooksByIdsAsync(It.IsAny<List<int>>())).ReturnsAsync(bookDict);

        // Act & Assert
        await FluentActions.Invoking(() => _orderService.CheckoutKioskAsync(request))
            .Should().ThrowAsync<System.Exception>()
            .WithMessage("*đã hết hàng hoặc không đủ số lượng*");
    }
}
