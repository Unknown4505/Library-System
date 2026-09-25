using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Books;

namespace BookKiosk.Application.Interfaces.Services;

public interface IBookService
{
    // Sử dụng ApiResponseDto chung đã được khai báo từ trước để response chuẩn format
    Task<ApiResponseDto<IEnumerable<BookDto>>> GetAllBooksAsync();
    Task<ApiResponseDto<BookDto>> GetBookByIdAsync(int id);
    Task<ApiResponseDto<BookDto>> GetBookByBarcodeAsync(string barcode);
    Task<ApiResponseDto<int>> CreateBookAsync(CreateBookDto dto);
    Task<ApiResponseDto<bool>> UpdateBookAsync(int id, UpdateBookDto dto);
    Task<ApiResponseDto<bool>> DeleteBookAsync(int id);
}
