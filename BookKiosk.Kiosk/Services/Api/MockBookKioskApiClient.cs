using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Kiosk.Services.Api;

public class MockBookKioskApiClient : IBookKioskApiClient
{
    private static int _nextOrderId = 1000;

    public async Task<ApiResponseDto<PaginatedResultDto<BookDto>>?> GetBooksAsync(int page = 1, int pageSize = 12, string? keyword = null, int? categoryId = null)
    {
        await Task.Delay(500); // Giả lập độ trễ mạng
        
        var mockBooks = new List<BookDto>
        {
            new BookDto { BookId = 1, Barcode = "8935244878342", Title = "Cây Cam Ngọt Của Tôi", Author = "José Mauro", SellingPrice = 105000, CategoryName = "Văn học", ImageUrl = "/uploads/books/cay-cam.jpg", AvailableStock = 15 },
            new BookDto { BookId = 2, Barcode = "9786043652873", Title = "Nhà Giả Kim", Author = "Paulo Coelho", SellingPrice = 79000, CategoryName = "Văn học", ImageUrl = "/uploads/books/nha-gia-kim.jpg", AvailableStock = 10 },
            new BookDto { BookId = 3, Barcode = "1234567890123", Title = "Clean Code", Author = "Robert C. Martin", SellingPrice = 300000, CategoryName = "Công nghệ", ImageUrl = "/uploads/books/clean-code.jpg", AvailableStock = 5 }
        };

        return ApiResponseDto<PaginatedResultDto<BookDto>>.Ok(
            new PaginatedResultDto<BookDto>
            {
                Items = mockBooks,
                TotalCount = 3,
                PageSize = pageSize,
                Page = page
            },
            "Lấy dữ liệu thành công (Mock)");
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByBarcodeAsync(string barcode)
    {
        await Task.Delay(500);
        return ApiResponseDto<BookDto>.Ok(new BookDto { BookId = 1, Barcode = barcode, Title = "Sách quét mã vạch", Author = "Tác giả Mock", SellingPrice = 150000, AvailableStock = 5, ImageUrl = "/uploads/books/cay-cam.jpg" });
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByIdAsync(int bookId)
    {
        await Task.Delay(500);
        return ApiResponseDto<BookDto>.Ok(new BookDto { BookId = bookId, Barcode = "8935244878342", Title = "Sách chi tiết", Author = "Tác giả Mock", SellingPrice = 150000, AvailableStock = 5, ImageUrl = "/uploads/books/cay-cam.jpg", AreaName = "Kệ A1" });
    }

    public async Task<ApiResponseDto<List<CategoryDto>>?> GetCategoriesAsync()
    {
        await Task.Delay(300);
        return ApiResponseDto<List<CategoryDto>>.Ok(
            new List<CategoryDto>
            {
                new CategoryDto { CategoryId = 1, Name = "Văn học" },
                new CategoryDto { CategoryId = 2, Name = "Kinh tế" },
                new CategoryDto { CategoryId = 3, Name = "Công nghệ" }
            });
    }

    public async Task<ApiResponseDto<List<AreaDto>>?> GetAreasAsync()
    {
        await Task.Delay(300);
        return ApiResponseDto<List<AreaDto>>.Ok(
            new List<AreaDto>
            {
                new AreaDto { AreaId = 1, Name = "Kệ A1 - Văn học" },
                new AreaDto { AreaId = 2, Name = "Kệ B2 - Kinh tế" }
            });
    }

    public async Task<ApiResponseDto<MemberDto>?> GetMemberByPhoneAsync(string phoneNumber)
    {
        await Task.Delay(500);
        if (phoneNumber == "0901234567")
        {
            return ApiResponseDto<MemberDto>.Ok(new MemberDto { MemberId = 1, FullName = "Nguyễn Văn Mock", PhoneNumber = phoneNumber, Points = 150 });
        }
        return ApiResponseDto<MemberDto>.Error("NOT_FOUND", "Không tìm thấy thành viên");
    }

    public async Task<ApiResponseDto<MemberDto>?> CreateMemberAsync(CreateMemberDto request)
    {
        await Task.Delay(500);
        return ApiResponseDto<MemberDto>.Ok(new MemberDto { MemberId = 2, FullName = request.FullName, PhoneNumber = request.PhoneNumber, Points = 0 });
    }

    public async Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request)
    {
        await Task.Delay(1000);
        var orderId = Interlocked.Increment(ref _nextOrderId);
        var orderCode = $"ORD-MOCK-{orderId}";
        const decimal subTotal = 300000m;
        var pointsUsedAmount = Math.Min(Math.Max(request.PointsToUse, 0) * 1000m, subTotal);
        var totalAmount = subTotal - pointsUsedAmount;

        return ApiResponseDto<CheckoutResponseDto>.Ok(
            new CheckoutResponseDto
            {
                OrderId = orderId,
                OrderCode = orderCode,
                SubTotal = subTotal,
                DiscountAmount = 0,
                PointsUsedAmount = pointsUsedAmount,
                TotalAmount = totalAmount,
                SepayQrCodeUrl = $"https://qr.sepay.vn/img?bank=Vietcombank&amount={totalAmount:0}&code={Uri.EscapeDataString(orderCode)}"
            },
            "Tạo đơn hàng thành công (Mock)");
    }

    public async Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId)
    {
        await Task.Delay(500);
        return ApiResponseDto<object>.Ok(new object(), "Đã huỷ đơn hàng (Mock)");
    }

    public async Task<ApiResponseDto<PaymentStatusDto>?> GetPaymentStatusAsync(int orderId)
    {
        await Task.Delay(500);
        return ApiResponseDto<PaymentStatusDto>.Ok(
            new PaymentStatusDto { OrderId = orderId, OrderCode = $"ORD-MOCK-{orderId}", Status = "Pending" });
    }

    public async Task<ApiResponseDto<ReceiptDto>?> GetReceiptAsync(int orderId, CancellationToken cancellationToken = default)
    {
        await Task.Delay(300, cancellationToken);
        var now = DateTime.UtcNow;
        var receipt = new ReceiptDto
        {
            OrderId = orderId,
            OrderCode = $"ORD-MOCK-{orderId}",
            CreatedAt = now.AddMinutes(-2),
            CompletedAt = now,
            SubTotal = 300000m,
            PromotionId = null,
            PromotionName = null,
            PromotionType = null,
            DiscountAmount = 0m,
            PointsUsed = 0,
            PointsUsedAmount = 0m,
            TotalAmount = 300000m,
            PaymentMethod = PaymentMethod.QR,
            Items = new List<ReceiptLineDto>
            {
                new ReceiptLineDto
                {
                    BookId = 1,
                    BookTitle = "Cây Cam Ngọt Của Tôi",
                    Quantity = 2,
                    UnitPriceAtTime = 105000m,
                    DiscountPerUnit = 0m,
                    LineTotal = 210000m,
                    LineDiscountAmount = 0m,
                    NetLineTotal = 210000m
                },
                new ReceiptLineDto
                {
                    BookId = 2,
                    BookTitle = "Nhà Giả Kim",
                    Quantity = 1,
                    UnitPriceAtTime = 90000m,
                    DiscountPerUnit = 0m,
                    LineTotal = 90000m,
                    LineDiscountAmount = 0m,
                    NetLineTotal = 90000m
                }
            }
        };

        return ApiResponseDto<ReceiptDto>.Ok(receipt, "Lấy hóa đơn thành công (Mock)");
    }

    public Task<ApiResponseDto<object>?> SendHeartbeatAsync(int kioskId, int status = 1, string? errorCode = null, string? errorMessage = null)
    {
        return Task.FromResult<ApiResponseDto<object>?>(
            ApiResponseDto<object>.Ok(new object(), "Mock heartbeat sent"));
    }

    public Task<ApiResponseDto<object>?> SendHeartbeatAsync(string kioskId)
    {
        return int.TryParse(kioskId, out var databaseId)
            ? SendHeartbeatAsync(databaseId)
            : Task.FromResult<ApiResponseDto<object>?>(
                ApiResponseDto<object>.Error("INVALID_KIOSK_ID", "Kiosk ID must be the numeric ID assigned by the database."));
    }

    public Task<ApiResponseDto<object>?> SimulatePaymentWebhookAsync(string orderCode, decimal amount)
    {
        return Task.FromResult<ApiResponseDto<object>?>(
            ApiResponseDto<object>.Ok(new object(), "Đã gửi Webhook giả lập (Mock)"));
    }

    public Task<ApiResponseDto<object>?> DevSimulatePaymentSuccessAsync(string orderCode)
    {
        return Task.FromResult<ApiResponseDto<object>?>(
            ApiResponseDto<object>.Ok(new object(), "Đã mô phỏng thanh toán thành công (DEV Mock)"));
    }

    // ── ADVERTISING MOCK ────────────────────────────────────────────────────────
    public bool MockAdvertisingShouldFail { get; set; } = false;
    public bool MockAdvertisingShouldPartial { get; set; } = false;
    public bool MockAdvertisingShouldEmpty { get; set; } = false;
    public bool MockAdvertisingShould503 { get; set; } = false;
    public bool MockAdvertisingShouldTimeout { get; set; } = false;

    public async Task<ApiResponseDto<KioskAdvertisingDto>?> GetAdvertisingAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        if (MockAdvertisingShouldTimeout)
        {
            throw new TaskCanceledException("Mô phỏng timeout kết nối quảng cáo");
        }

        if (MockAdvertisingShould503)
        {
            return ApiResponseDto<KioskAdvertisingDto>.Error("SERVICE_UNAVAILABLE", "Nội dung quảng cáo tạm thời không khả dụng.");
        }

        if (MockAdvertisingShouldFail)
        {
            return ApiResponseDto<KioskAdvertisingDto>.Error("NETWORK_ERROR", "Lỗi kết nối máy chủ mô phỏng.");
        }

        if (MockAdvertisingShouldEmpty)
        {
            return ApiResponseDto<KioskAdvertisingDto>.Ok(new KioskAdvertisingDto(), "Không có dữ liệu quảng cáo.");
        }

        var dto = new KioskAdvertisingDto
        {
            ActivePromotions = new List<AdvertisingPromotionDto>
            {
                new AdvertisingPromotionDto
                {
                    PromotionId = 1,
                    Name = "Ưu Đãi Đặc Biệt Tháng Này",
                    Description = "Giảm ngay 50.000đ cho đơn hàng thanh toán tại Kiosk từ 200.000đ",
                    PromotionType = PromotionType.OrderDiscount,
                    OrderDiscount = new AdvertisingOrderDiscountDto
                    {
                        MinOrderValue = 200000m,
                        DiscountAmount = 50000m
                    },
                    StartDate = DateTime.UtcNow.AddDays(-7),
                    EndDate = DateTime.UtcNow.AddDays(14)
                }
            },
            NewArrivals = new List<AdvertisingNewArrivalDto>
            {
                new AdvertisingNewArrivalDto
                {
                    BookId = 1,
                    Title = "Cây Cam Ngọt Của Tôi",
                    ImageUrl = "/uploads/books/cay-cam.jpg",
                    SellingPrice = 105000m,
                    AvailableStock = 15,
                    LastImportedAt = DateTime.UtcNow.AddDays(-2)
                },
                new AdvertisingNewArrivalDto
                {
                    BookId = 2,
                    Title = "Nhà Giả Kim",
                    ImageUrl = "/uploads/books/nha-gia-kim.jpg",
                    SellingPrice = 79000m,
                    AvailableStock = 10,
                    LastImportedAt = DateTime.UtcNow.AddDays(-5)
                },
                new AdvertisingNewArrivalDto
                {
                    BookId = 3,
                    Title = "Clean Code: A Handbook of Agile Software Craftsmanship",
                    ImageUrl = "/uploads/books/clean-code.jpg",
                    SellingPrice = 300000m,
                    AvailableStock = 5,
                    LastImportedAt = DateTime.UtcNow.AddDays(-8)
                },
                new AdvertisingNewArrivalDto
                {
                    BookId = 4,
                    Title = "Đắc Nhân Tâm",
                    ImageUrl = "/uploads/books/dac-nhan-tam.jpg",
                    SellingPrice = 86000m,
                    AvailableStock = 20,
                    LastImportedAt = DateTime.UtcNow.AddDays(-12)
                }
            },
            BestSellers = new List<AdvertisingBestSellerDto>
            {
                new AdvertisingBestSellerDto
                {
                    BookId = 2,
                    Title = "Nhà Giả Kim",
                    ImageUrl = "/uploads/books/nha-gia-kim.jpg",
                    SellingPrice = 79000m,
                    AvailableStock = 10,
                    TotalSold = 152
                },
                new AdvertisingBestSellerDto
                {
                    BookId = 1,
                    Title = "Cây Cam Ngọt Của Tôi",
                    ImageUrl = "/uploads/books/cay-cam.jpg",
                    SellingPrice = 105000m,
                    AvailableStock = 15,
                    TotalSold = 98
                },
                new AdvertisingBestSellerDto
                {
                    BookId = 4,
                    Title = "Đắc Nhân Tâm",
                    ImageUrl = "/uploads/books/dac-nhan-tam.jpg",
                    SellingPrice = 86000m,
                    AvailableStock = 20,
                    TotalSold = 85
                },
                new AdvertisingBestSellerDto
                {
                    BookId = 3,
                    Title = "Clean Code",
                    ImageUrl = "/uploads/books/clean-code.jpg",
                    SellingPrice = 300000m,
                    AvailableStock = 5,
                    TotalSold = 42
                }
            }
        };

        if (MockAdvertisingShouldPartial)
        {
            dto.ActivePromotions.Clear();
            dto.UnavailableSections.Add("activePromotions");
            return new ApiResponseDto<KioskAdvertisingDto>
            {
                Success = true,
                Code = "PARTIAL_SUCCESS",
                Message = "Một số nội dung quảng cáo tạm thời không khả dụng.",
                Data = dto
            };
        }

        return ApiResponseDto<KioskAdvertisingDto>.Ok(dto, "Lấy dữ liệu quảng cáo thành công (Mock)");
    }
}
