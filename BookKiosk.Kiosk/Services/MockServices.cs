using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BookKiosk.Application.DTOs.Members;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Hardware;

namespace BookKiosk.Kiosk.Models
{
    public static class MockDataStore
    {
        public static List<CategoryModel> Categories { get; } = new List<CategoryModel>
        {
            new CategoryModel { Id = 1, Name = "Văn học" },
            new CategoryModel { Id = 2, Name = "Kinh tế" },
            new CategoryModel { Id = 3, Name = "Thiếu nhi" },
            new CategoryModel { Id = 4, Name = "Tâm lý học" },
            new CategoryModel { Id = 5, Name = "Khoa học" }
        };

        public static List<BookModel> Books { get; } = new List<BookModel>
        {
            new BookModel { BookId = 1, Barcode = "10000001", Title = "Đắc Nhân Tâm", Author = "Dale Carnegie", SellingPrice = 85000, ImageUrl = "https://picsum.photos/seed/book1/300/450", CategoryId = 4, AreaName = "Kệ A1", AvailableStock = 15 },
            new BookModel { BookId = 2, Barcode = "10000002", Title = "Nhà Giả Kim", Author = "Paulo Coelho", SellingPrice = 79000, ImageUrl = "https://picsum.photos/seed/book2/300/450", CategoryId = 1, AreaName = "Kệ B2", AvailableStock = 0 },
            new BookModel { BookId = 3, Barcode = "10000003", Title = "Dế Mèn Phiêu Lưu Ký", Author = "Tô Hoài", SellingPrice = 45000, ImageUrl = "https://picsum.photos/seed/book3/300/450", CategoryId = 3, AreaName = "Kệ C1", AvailableStock = 20 },
            new BookModel { BookId = 4, Barcode = "10000004", Title = "Sapiens - Lược sử loài người", Author = "Yuval Noah Harari", SellingPrice = 150000, ImageUrl = "https://picsum.photos/seed/book4/300/450", CategoryId = 5, AreaName = "Kệ A2", AvailableStock = 5 },
            new BookModel { BookId = 5, Barcode = "10000005", Title = "Tư Duy Nhanh Và Chậm", Author = "Daniel Kahneman", SellingPrice = 125000, ImageUrl = "https://picsum.photos/seed/book5/300/450", CategoryId = 4, AreaName = "Kệ A1", AvailableStock = 10 },
            new BookModel { BookId = 6, Barcode = "10000006", Title = "Cha Giàu Cha Nghèo", Author = "Robert Kiyosaki", SellingPrice = 110000, ImageUrl = "https://picsum.photos/seed/book6/300/450", CategoryId = 2, AreaName = "Kệ D1", AvailableStock = 30 },
            new BookModel { BookId = 7, Barcode = "10000007", Title = "Tôi Thấy Hoa Vàng Trên Cỏ Xanh", Author = "Nguyễn Nhật Ánh", SellingPrice = 95000, ImageUrl = "https://picsum.photos/seed/book7/300/450", CategoryId = 1, AreaName = "Kệ B1", AvailableStock = 12 },
            new BookModel { BookId = 8, Barcode = "10000008", Title = "Nghĩ Giàu Làm Giàu", Author = "Napoleon Hill", SellingPrice = 89000, ImageUrl = "https://picsum.photos/seed/book8/300/450", CategoryId = 2, AreaName = "Kệ D2", AvailableStock = 8 }
        };

        public static List<MemberDto> Members { get; } = new List<MemberDto>
        {
            new MemberDto { MemberId = 1, PhoneNumber = "0912345678", FullName = "Nguyễn Văn A", Points = 500 },
            new MemberDto { MemberId = 2, PhoneNumber = "0987654321", FullName = "Trần Thị B", Points = 1200 },
            new MemberDto { MemberId = 3, PhoneNumber = "0909090909", FullName = "Lê Văn C", Points = 0 },
            new MemberDto { MemberId = 4, PhoneNumber = "0123456789", FullName = "Phạm Thị D", Points = 350 }
        };
    }
}

namespace BookKiosk.Kiosk.Services
{
    public class MockBookService : IBookService
    {
        private readonly List<CategoryModel> _categories;
        private readonly List<BookModel> _books;

        public MockBookService()
        {
            _categories = MockDataStore.Categories;
            _books = MockDataStore.Books;
        }

        public async Task<List<CategoryModel>> GetCategoriesAsync()
        {
            await Task.Delay(200); // Simulate network latency
            return _categories;
        }

        public async Task<List<BookModel>> GetFeaturedBooksAsync()
        {
            await Task.Delay(300);
            return _books.Take(4).ToList();
        }

        public async Task<List<BookModel>> SearchBooksAsync(string keyword, int? categoryId, int skip, int take)
        {
            await Task.Delay(400);
            var query = _books.AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(b => b.Title.ToLower().Contains(keyword.ToLower()) || b.Author.ToLower().Contains(keyword.ToLower()));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(b => b.CategoryId == categoryId.Value);
            }

            return query.Skip(skip).Take(take).ToList();
        }

        public async Task<BookModel> GetBookByIdAsync(int bookId)
        {
            await Task.Delay(200);
            return _books.FirstOrDefault(b => b.BookId == bookId);
        }
    }
}

namespace BookKiosk.Kiosk.Services.Hardware
{
    public class MockBarcodeScanner : IBarcodeScanner
    {
        public event EventHandler<string>? BarcodeScanned;

        public void StartListening()
        {
            Debug.WriteLine("[Mock Scanner] Đã bật chế độ lắng nghe mã vạch.");
        }

        public void StopListening()
        {
            Debug.WriteLine("[Mock Scanner] Đã tắt chế độ lắng nghe mã vạch.");
        }

        /// <summary>
        /// Hàm này chỉ dùng cho Dev để giả lập việc quét mã vạch bằng cách 
        /// gọi hàm này từ nút bấm trên UI hoặc gõ phím trên Laptop.
        /// </summary>
        public void SimulateScan(string mockBarcode)
        {
            Debug.WriteLine($"[Mock Scanner] Tít! Mã vạch nhận được: {mockBarcode}");
            BarcodeScanned?.Invoke(this, mockBarcode);
        }
    }

    public class MockReceiptPrinter : IReceiptPrinter
    {
        public async Task<bool> PrintReceiptAsync(string orderCode, decimal totalAmount)
        {
            // Giả lập độ trễ của máy in cơ học thật (1.5 giây)
            await Task.Delay(1500);

            // TODO: @lehuukhang (Khang) - Bạn tự viết logic tạo Mock Data in hóa đơn (Debug.WriteLine) ở đây nhé.
            // Việc tự viết sẽ giúp bạn nắm rõ các trường dữ liệu cần in ra UI sau này.
            // Gợi ý: In ra Mã đơn (orderCode) và Tổng tiền (totalAmount).
            
            return true;
        }
    }
}
