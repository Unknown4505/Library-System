using BookKiosk.Domain.Entities;

namespace BookKiosk.Application.Interfaces.Repositories;

public interface IBookRepository
{
    Task<IEnumerable<Book>> GetAllAsync();
    Task<Book?> GetByIdAsync(int id);
    Task<Book?> GetByBarcodeAsync(string barcode);
    Task<bool> IsBarcodeExistsAsync(string barcode);
    Task AddAsync(Book book);
    Task UpdateAsync(Book book);
    Task DeleteAsync(Book book);
}
