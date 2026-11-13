using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.Services;
using BookKiosk.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Infrastructure.Repositories;

public sealed class BookBarcodeRepository(ApplicationDbContext context) : IBookBarcodeRepository
{
    public async Task<ApiResponseDto<ChangeBarcodeDto>> ChangeAsync(int bookId, string newBarcode)
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var book = await context.Books.AsNoTracking().SingleOrDefaultAsync(b => b.BookId == bookId);
            if (book == null)
                return Error("NOT_FOUND", "Không tìm thấy sách.");
            var storedForm = await context.Database.SqlQuery<string>(
                $"SELECT CONVERT(nvarchar(50), CONVERT(varchar(50), {newBarcode})) AS [Value]").SingleAsync();
            if (!string.Equals(storedForm, newBarcode, StringComparison.Ordinal))
                return Error("BAD_REQUEST", "Mã mới không tương thích với định dạng varchar(50) của cơ sở dữ liệu.");
            if (string.Equals(book.Barcode.Trim(), newBarcode, StringComparison.Ordinal) ||
                await context.Books.AnyAsync(b => b.BookId == bookId && b.Barcode == newBarcode))
                return Error("BAD_REQUEST", "Mã mới phải khác mã hiện tại.");
            if (book.ReservedQuantity != 0)
                return Error("CONFLICT", "Không thể đổi mã khi sách đang được giữ chỗ. Vui lòng hoàn tất hoặc hủy đơn trước.");
            if (await context.Books.AnyAsync(b => b.BookId != bookId && b.Barcode == newBarcode))
                return Error("CONFLICT", "Mã vạch mới đã được sử dụng.");

            var changed = await context.Books
                .Where(b => b.BookId == bookId && b.Barcode == book.Barcode && b.ReservedQuantity == 0)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Barcode, newBarcode));
            if (changed != 1)
                return Error("CONFLICT", "Thông tin sách hoặc lượng giữ chỗ đã thay đổi. Vui lòng tải lại và thử lại.");

            var changedAt = DateTime.UtcNow;
            await transaction.CommitAsync();
            return ApiResponseDto<ChangeBarcodeDto>.Ok(new(bookId, book.Barcode, newBarcode, true, changedAt),
                "Đổi mã vạch thành công. Vui lòng in và dán tem mã mới ngay.");
        }
        catch (Exception ex) when (IsSqlUniqueViolation(ex))
        {
            // The transaction is disposed/rolled back before this scope exits.
            return Error("CONFLICT", "Mã vạch mới đã được sử dụng hoặc bị thay đổi đồng thời.");
        }
        // Other failures propagate to the existing safe global error handler.
        // await using rolls back an uncommitted transaction on every early return/failure.
    }

    private static ApiResponseDto<ChangeBarcodeDto> Error(string code, string message) =>
        ApiResponseDto<ChangeBarcodeDto>.Error(code, message);

    private static bool IsSqlUniqueViolation(Exception error)
    {
        for (Exception? current = error; current != null; current = current.InnerException)
            if (current is SqlException sql && sql.Number is 2601 or 2627) return true;
        return false;
    }
}
