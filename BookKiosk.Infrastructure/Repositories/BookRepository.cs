using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Infrastructure.Repositories;

public class BookRepository : IBookRepository
{
    private readonly ApplicationDbContext _context;

    public BookRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Book>> GetAllAsync()
    {
        // AsNoTracking() giúp lấy data ra siêu nhanh vì EF không cần theo dõi sự thay đổi của Object
        // Include để lấy luôn thông tin Category và Area (JOIN)
        return await _context.Books
            .Include(b => b.Category)
            .Include(b => b.Area)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<(IEnumerable<Book> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? keyword, int? categoryId)
    {
        var query = _context.Books
            .Include(b => b.Category)
            .Include(b => b.Area)
            .Where(b => b.IsActive) // Kiosk chỉ thấy sách đang kinh doanh
            .AsNoTracking();

        // Lọc theo từ khoá (tiêu đề hoặc tác giả)
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(b =>
                b.Title.ToLower().Contains(kw) ||
                b.Author.ToLower().Contains(kw));
        }

        // Lọc theo danh mục
        if (categoryId.HasValue)
            query = query.Where(b => b.CategoryId == categoryId.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(b => b.Title) // Sắp xếp nhất quán — tránh random order mỗi lần load
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Book?> GetByIdAsync(int id)
    {
        return await _context.Books
            .Include(b => b.Category)
            .Include(b => b.Area)
            .FirstOrDefaultAsync(b => b.BookId == id);
    }

    public async Task<Book?> GetByBarcodeAsync(string barcode)
    {
        return await _context.Books
            .Include(b => b.Category)
            .Include(b => b.Area)
            .FirstOrDefaultAsync(b => b.Barcode == barcode);
    }

    public async Task<bool> IsBarcodeExistsAsync(string barcode)
    {
        return await _context.Books.AnyAsync(b => b.Barcode == barcode);
    }

    public async Task AddAsync(Book book)
    {
        await _context.Books.AddAsync(book);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Book book)
    {
        _context.Books.Update(book);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Book book)
    {
        _context.Books.Remove(book);
        await _context.SaveChangesAsync();
    }
}
