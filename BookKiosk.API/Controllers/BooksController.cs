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

    private readonly IWebHostEnvironment _environment;

    public BooksController(IBookService bookService, IWebHostEnvironment environment)
    {
        _bookService = bookService;
        _environment = environment;
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
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("book-cover-upload")]
    [RequestSizeLimit(Services.BookCoverImageValidator.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Services.BookCoverImageValidator.MaxRequestBytes,
        MemoryBufferThreshold = 64 * 1024, MultipartHeadersLengthLimit = 8 * 1024,
        MultipartHeadersCountLimit = 16, ValueCountLimit = 8, ValueLengthLimit = 8 * 1024, KeyLengthLimit = 128)]
    public async Task<IActionResult> UploadImage(Microsoft.AspNetCore.Http.IFormFile file)
    {
        var image = file == null ? null : await Services.BookCoverImageValidator.NormalizeAsync(file, HttpContext.RequestAborted);
        if (image is null)
            return BadRequest(ApiResponseDto<object>.Error("BAD_REQUEST", "Ảnh bìa phải là JPG, PNG hoặc WEBP hợp lệ, tối đa 5 MB và 16 triệu điểm ảnh."));

        var extension = Path.GetExtension(file!.FileName).ToLowerInvariant();
        var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "books");
        Directory.CreateDirectory(uploadsFolder);
        var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        var created = false;
        try
        {
            await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            created = true;
            await stream.WriteAsync(image, HttpContext.RequestAborted);
        }
        catch
        {
            if (created) System.IO.File.Delete(filePath);
            throw;
        }

        var fileUrl = $"/uploads/books/{uniqueFileName}";
        return Ok(ApiResponseDto<string>.Ok(fileUrl, "Tải ảnh bìa thành công."));
    }

#if DEBUG
    // [TEST LỖI] - Chỉ dùng để test Global Exception Handler
    [HttpGet("test-error")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult TestError()
    {
        // Cố tình quăng lỗi để xem khiên bảo vệ có bắt được không
        throw new DivideByZeroException("Cố tình chia cho 0 để test khiên bảo vệ Server!");
    }
#endif

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
