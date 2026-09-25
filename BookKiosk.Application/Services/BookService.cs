using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Interfaces.Services;
using BookKiosk.Domain.Entities;

namespace BookKiosk.Application.Services;

public class BookService : IBookService
{
    private readonly IBookRepository _bookRepository;

    public BookService(IBookRepository bookRepository)
    {
        _bookRepository = bookRepository;
    }

    public async Task<ApiResponseDto<IEnumerable<BookDto>>> GetAllBooksAsync()
    {
        var books = await _bookRepository.GetAllAsync();
        
        var dtos = books.Select(MapToDto).ToList();
        
        return ApiResponseDto<IEnumerable<BookDto>>.Ok(dtos);
    }

    public async Task<ApiResponseDto<BookDto>> GetBookByIdAsync(int id)
    {
        var book = await _bookRepository.GetByIdAsync(id);
        if (book == null)
        {
            return ApiResponseDto<BookDto>.Error("NOT_FOUND", $"Không tìm thấy sách với ID = {id}");
        }

        return ApiResponseDto<BookDto>.Ok(MapToDto(book));
    }

    public async Task<ApiResponseDto<BookDto>> GetBookByBarcodeAsync(string barcode)
    {
        var book = await _bookRepository.GetByBarcodeAsync(barcode);
        if (book == null)
        {
            return ApiResponseDto<BookDto>.Error("NOT_FOUND", $"Không tìm thấy sách với mã vạch = {barcode}");
        }

        return ApiResponseDto<BookDto>.Ok(MapToDto(book));
    }

    public async Task<ApiResponseDto<int>> CreateBookAsync(CreateBookDto dto)
    {
        // 1. Kiểm tra trùng Barcode
        var isExists = await _bookRepository.IsBarcodeExistsAsync(dto.Barcode);
        if (isExists)
        {
            return ApiResponseDto<int>.Error("BAD_REQUEST", "Mã vạch này đã tồn tại trong hệ thống.");
        }

        // 2. Map từ Dto sang Entity
        var book = new Book
        {
            Barcode = dto.Barcode,
            Title = dto.Title,
            Author = dto.Author,
            Publisher = dto.Publisher,
            ImageUrl = dto.ImageUrl,
            CostPrice = dto.CostPrice,
            SellingPrice = dto.SellingPrice,
            StockQuantity = dto.StockQuantity,
            ReservedQuantity = 0,
            CategoryId = dto.CategoryId,
            AreaId = dto.AreaId,
            IsActive = dto.IsActive
        };

        // 3. Lưu vào Database
        await _bookRepository.AddAsync(book);

        return ApiResponseDto<int>.Ok(book.BookId, "Thêm sách thành công.");
    }

    public async Task<ApiResponseDto<bool>> UpdateBookAsync(int id, UpdateBookDto dto)
    {
        var book = await _bookRepository.GetByIdAsync(id);
        if (book == null)
        {
            return ApiResponseDto<bool>.Error("NOT_FOUND", $"Không tìm thấy sách với ID = {id}");
        }

        book.Title = dto.Title;
        book.Author = dto.Author;
        book.Publisher = dto.Publisher;
        book.ImageUrl = dto.ImageUrl;
        book.CostPrice = dto.CostPrice;
        book.SellingPrice = dto.SellingPrice;
        book.StockQuantity = dto.StockQuantity;
        book.CategoryId = dto.CategoryId;
        book.AreaId = dto.AreaId;
        book.IsActive = dto.IsActive;

        await _bookRepository.UpdateAsync(book);

        return ApiResponseDto<bool>.Ok(true, "Cập nhật sách thành công.");
    }

    public async Task<ApiResponseDto<bool>> DeleteBookAsync(int id)
    {
        var book = await _bookRepository.GetByIdAsync(id);
        if (book == null)
        {
            return ApiResponseDto<bool>.Error("NOT_FOUND", $"Không tìm thấy sách với ID = {id}");
        }

        // Soft Delete (Chỉ ẩn đi chứ không xóa khỏi Database)
        book.IsActive = false;
        await _bookRepository.UpdateAsync(book);

        return ApiResponseDto<bool>.Ok(true, "Xóa (ẩn) sách thành công.");
    }

    // Hàm phụ trợ ánh xạ thủ công để đảm bảo code tường minh, không cần xài thư viện AutoMapper phức tạp
    private static BookDto MapToDto(Book book)
    {
        return new BookDto
        {
            BookId = book.BookId,
            Barcode = book.Barcode,
            Title = book.Title,
            Author = book.Author,
            Publisher = book.Publisher,
            ImageUrl = book.ImageUrl,
            CostPrice = book.CostPrice,
            SellingPrice = book.SellingPrice,
            StockQuantity = book.StockQuantity,
            ReservedQuantity = book.ReservedQuantity,
            AvailableStock = book.AvailableStock, // Gọi property tính toán sẵn trong Entity
            IsActive = book.IsActive,
            CategoryId = book.CategoryId,
            CategoryName = book.Category?.Name ?? string.Empty,
            AreaId = book.AreaId,
            AreaName = book.Area?.Name
        };
    }
}
