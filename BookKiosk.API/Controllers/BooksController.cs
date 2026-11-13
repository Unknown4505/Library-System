using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllBooks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? keyword = null,
        [FromQuery] int? categoryId = null)
    {
        var response = await _bookService.GetBooksAsync(page, pageSize, keyword, categoryId);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBookById(int id)
    {
        var response = await _bookService.GetBookByIdAsync(id);
        if (!response.Success)
            return NotFound(response);

        // Kiosk/public không được mở trực tiếp sách đã ngừng kinh doanh.
        // Admin CMS vẫn cần đọc bản ghi inactive để chỉnh sửa/kích hoạt lại.
        if (response.Data is { IsActive: false } && !User.IsInRole("Admin"))
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", "Không tìm thấy sách hoặc sách đã ngừng kinh doanh."));
            
        return Ok(response);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetBookByBarcode(string barcode)
    {
        var response = await _bookService.GetBookByBarcodeAsync(barcode);
        if (!response.Success)
            return NotFound(response);

        if (response.Data is { IsActive: false } && !User.IsInRole("Admin"))
            return NotFound(ApiResponseDto<object>.Error("NOT_FOUND", "Không tìm thấy sách hoặc sách đã ngừng kinh doanh."));
            
        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateBook([FromBody] CreateBookDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ValidationError());

        var response = await _bookService.CreateBookAsync(dto);
        if (!response.Success)
            return BadRequest(response);

        return CreatedAtAction(nameof(GetBookById), new { id = response.Data }, response);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateBook(int id, [FromBody] UpdateBookDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ValidationError());

        var response = await _bookService.UpdateBookAsync(id, dto);
        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        var response = await _bookService.DeleteBookAsync(id);
        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }

    [HttpPost("upload-image")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadImage(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", "File ảnh không hợp lệ."));

        const long maxBytes = 5 * 1024 * 1024;
        if (file.Length > maxBytes)
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", "Ảnh bìa không được vượt quá 5 MB."));

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        var allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };
        if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(file.ContentType))
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", "Chỉ chấp nhận ảnh JPG, PNG hoặc WEBP."));

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "books");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileUrl = $"/uploads/books/{uniqueFileName}";
        return Ok(ApiResponseDto<string>.Ok(fileUrl, "Tải ảnh bìa thành công."));
    }

    private ApiResponseDto<object> ValidationError()
    {
        var message = string.Join("; ", ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .Where(error => !string.IsNullOrWhiteSpace(error)));

        return ApiResponseDto<object>.Error(
            "VALIDATION_ERROR",
            string.IsNullOrWhiteSpace(message) ? "Dữ liệu sách không hợp lệ." : message);
    }
}
