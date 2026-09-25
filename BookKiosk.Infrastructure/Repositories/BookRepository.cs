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
