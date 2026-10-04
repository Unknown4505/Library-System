using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookKiosk.Kiosk.Models;

namespace BookKiosk.Kiosk.Services
{
    public class MockBookService : IBookService
    {
        private readonly List<CategoryModel> _categories;
        private readonly List<BookModel> _books;

        public MockBookService()
        {
            _categories = new List<CategoryModel>
            {
                new CategoryModel { Id = 1, Name = "Văn học" },
                new CategoryModel { Id = 2, Name = "Kinh tế" },
                new CategoryModel { Id = 3, Name = "Thiếu nhi" }
            };

            _books = new List<BookModel>
            {
                new BookModel { BookId = 1, Barcode = "10000001", Title = "Đắc Nhân Tâm", Author = "Dale Carnegie", SellingPrice = 85000, ImageUrl = "https://picsum.photos/seed/book1/300/450", CategoryId = 2, AreaName = "Kệ A1", AvailableStock = 15 },
                new BookModel { BookId = 2, Barcode = "10000002", Title = "Nhà Giả Kim", Author = "Paulo Coelho", SellingPrice = 79000, ImageUrl = "https://picsum.photos/seed/book2/300/450", CategoryId = 1, AreaName = "Kệ B2", AvailableStock = 0 },
                new BookModel { BookId = 3, Barcode = "10000003", Title = "Dế Mèn Phiêu Lưu Ký", Author = "Tô Hoài", SellingPrice = 45000, ImageUrl = "https://picsum.photos/seed/book3/300/450", CategoryId = 3, AreaName = "Kệ C1", AvailableStock = 20 },
                new BookModel { BookId = 4, Barcode = "10000004", Title = "Sapiens - Lược sử loài người", Author = "Yuval Noah Harari", SellingPrice = 150000, ImageUrl = "https://picsum.photos/seed/book4/300/450", CategoryId = 1, AreaName = "Kệ A2", AvailableStock = 5 },
                new BookModel { BookId = 5, Barcode = "10000005", Title = "Tư Duy Nhanh Và Chậm", Author = "Daniel Kahneman", SellingPrice = 125000, ImageUrl = "https://picsum.photos/seed/book5/300/450", CategoryId = 2, AreaName = "Kệ A1", AvailableStock = 10 }
            };
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
