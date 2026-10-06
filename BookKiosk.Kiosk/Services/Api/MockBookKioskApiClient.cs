using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.Services.Api;

public class MockBookKioskApiClient : IBookKioskApiClient
{
    public async Task<ApiResponseDto<PaginatedResultDto<BookDto>>?> GetBooksAsync(int page = 1, int pageSize = 12, string? keyword = null, int? categoryId = null)
    {
        await Task.Delay(500); // Giả lập độ trễ mạng
        
        var mockBooks = new List<BookDto>
        {
            new BookDto { BookId = 1, Barcode = "8935244878342", Title = "Cây Cam Ngọt Của Tôi", Author = "José Mauro", SellingPrice = 105000, CategoryName = "Văn học", ImageUrl = "/uploads/books/cay-cam.jpg", AvailableStock = 15 },
            new BookDto { BookId = 2, Barcode = "9786043652873", Title = "Nhà Giả Kim", Author = "Paulo Coelho", SellingPrice = 79000, CategoryName = "Văn học", ImageUrl = "/uploads/books/nha-gia-kim.jpg", AvailableStock = 10 },
            new BookDto { BookId = 3, Barcode = "1234567890123", Title = "Clean Code", Author = "Robert C. Martin", SellingPrice = 300000, CategoryName = "Công nghệ", ImageUrl = "/uploads/books/clean-code.jpg", AvailableStock = 5 }
        };

        return new ApiResponseDto<PaginatedResultDto<BookDto>>
        {
            Success = true,
            Message = "Lấy dữ liệu thành công (Mock)",
            Data = new PaginatedResultDto<BookDto>
            {
                Items = mockBooks,
                TotalCount = 3,
                PageSize = pageSize,
                Page = page
            }
        };
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByBarcodeAsync(string barcode)
    {
        await Task.Delay(500);
        return new ApiResponseDto<BookDto>
        {
            Success = true,
            Data = new BookDto { BookId = 1, Barcode = barcode, Title = "Sách quét mã vạch", Author = "Tác giả Mock", SellingPrice = 150000, AvailableStock = 5, ImageUrl = "/uploads/books/cay-cam.jpg" }
        };
    }

    public async Task<ApiResponseDto<BookDto>?> GetBookByIdAsync(int bookId)
    {
        await Task.Delay(500);
        return new ApiResponseDto<BookDto>
        {
            Success = true,
            Data = new BookDto { BookId = bookId, Barcode = "8935244878342", Title = "Sách chi tiết", Author = "Tác giả Mock", SellingPrice = 150000, AvailableStock = 5, ImageUrl = "/uploads/books/cay-cam.jpg", AreaName = "Kệ A1" }
        };
    }

    public async Task<ApiResponseDto<List<CategoryDto>>?> GetCategoriesAsync()
    {
        await Task.Delay(300);
        return new ApiResponseDto<List<CategoryDto>>
        {
            Success = true,
            Data = new List<CategoryDto>
            {
                new CategoryDto { CategoryId = 1, Name = "Văn học" },
                new CategoryDto { CategoryId = 2, Name = "Kinh tế" },
                new CategoryDto { CategoryId = 3, Name = "Công nghệ" }
            }
        };
    }

    public async Task<ApiResponseDto<List<AreaDto>>?> GetAreasAsync()
    {
        await Task.Delay(300);
        return new ApiResponseDto<List<AreaDto>>
        {
            Success = true,
            Data = new List<AreaDto>
            {
                new AreaDto { AreaId = 1, Name = "Kệ A1 - Văn học" },
                new AreaDto { AreaId = 2, Name = "Kệ B2 - Kinh tế" }
            }
        };
    }

    public async Task<ApiResponseDto<MemberDto>?> GetMemberByPhoneAsync(string phoneNumber)
    {
        await Task.Delay(500);
        if (phoneNumber == "0901234567")
        {
            return new ApiResponseDto<MemberDto>
            {
                Success = true,
                Data = new MemberDto { MemberId = 1, FullName = "Nguyễn Văn Mock", PhoneNumber = phoneNumber, Points = 150 }
            };
        }
        return new ApiResponseDto<MemberDto> { Success = false, Message = "Không tìm thấy thành viên" };
    }

    public async Task<ApiResponseDto<MemberDto>?> CreateMemberAsync(CreateMemberDto request)
    {
        await Task.Delay(500);
        return new ApiResponseDto<MemberDto>
        {
            Success = true,
            Data = new MemberDto { MemberId = 2, FullName = request.FullName, PhoneNumber = request.PhoneNumber, Points = 0 }
        };
    }

    public async Task<ApiResponseDto<CheckoutResponseDto>?> CheckoutAsync(CheckoutRequestDto request)
    {
        await Task.Delay(1000);
        return new ApiResponseDto<CheckoutResponseDto>
        {
            Success = true,
            Message = "Tạo đơn hàng thành công (Mock)",
            Data = new CheckoutResponseDto
            {
                OrderId = 999,
                OrderCode = "ORD-MOCK-999",
                SubTotal = 300000,
                DiscountAmount = 0,
                PointsUsedAmount = request.PointsToUse * 1000,
                TotalAmount = 300000 - (request.PointsToUse * 1000),
                SepayQrCodeUrl = "https://qr.sepay.vn/img?bank=Vietcombank&amount=300000&code=ORD-MOCK-999"
            }
        };
    }

    public async Task<ApiResponseDto<object>?> CancelOrderAsync(int orderId)
    {
        await Task.Delay(500);
        return new ApiResponseDto<object> { Success = true, Message = "Đã huỷ đơn hàng" };
    }

    public async Task<ApiResponseDto<PaymentStatusDto>?> GetPaymentStatusAsync(int orderId)
    {
        await Task.Delay(500);
        return new ApiResponseDto<PaymentStatusDto>
        {
            Success = true,
            Data = new PaymentStatusDto { OrderId = orderId, OrderCode = "ORD-MOCK-999", Status = "Pending" } 
        };
    }

    public async Task<ApiResponseDto<object>?> SendHeartbeatAsync(string kioskId)
    {
        return new ApiResponseDto<object> { Success = true };
    }
}
