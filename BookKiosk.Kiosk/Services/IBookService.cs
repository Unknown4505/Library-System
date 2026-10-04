using System.Collections.Generic;
using System.Threading.Tasks;
using BookKiosk.Kiosk.Models;

namespace BookKiosk.Kiosk.Services
{
    public interface IBookService
    {
        Task<List<CategoryModel>> GetCategoriesAsync();
        Task<List<BookModel>> GetFeaturedBooksAsync();
        Task<List<BookModel>> SearchBooksAsync(string keyword, int? categoryId, int skip, int take);
        Task<BookModel> GetBookByIdAsync(int bookId);
    }
}
