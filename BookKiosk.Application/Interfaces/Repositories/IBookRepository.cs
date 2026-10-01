using BookKiosk.Domain.Entities;

namespace BookKiosk.Application.Interfaces.Repositories;

public interface IBookRepository
{
    Task<IEnumerable<Book>> GetAllAsync();

    /// <summary>
    /// Phân trang + tìm kiếm theo tiêu đề/tác giả + lọc theo danh mục.
    /// Trả về (items, totalCount) để Service tạo PaginatedResultDto.
    /// </summary>
    Task<(IEnumerable<Book> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? keyword, int? categoryId);

    Task<Book?> GetByIdAsync(int id);
    Task<Book?> GetByBarcodeAsync(string barcode);
    Task<bool> IsBarcodeExistsAsync(string barcode);
    Task AddAsync(Book book);
    Task UpdateAsync(Book book);
    Task DeleteAsync(Book book);
}

