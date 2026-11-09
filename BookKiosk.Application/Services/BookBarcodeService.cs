using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.DTOs.Common;
using Microsoft.Extensions.Logging;

namespace BookKiosk.Application.Services;

public interface IBookBarcodeRepository
{
    Task<ApiResponseDto<ChangeBarcodeDto>> ChangeAsync(int bookId, string newBarcode);
}

public interface IBookBarcodeService
{
    Task<ApiResponseDto<ChangeBarcodeDto>> ChangeAsync(int bookId, ChangeBarcodeRequestDto request,
        int actorUserId, string correlationId);
}

public sealed class BookBarcodeService(IBookBarcodeRepository repository, ILogger<BookBarcodeService> logger) : IBookBarcodeService
{
    public async Task<ApiResponseDto<ChangeBarcodeDto>> ChangeAsync(int bookId, ChangeBarcodeRequestDto request,
        int actorUserId, string correlationId)
    {
        if (actorUserId <= 0)
            return ApiResponseDto<ChangeBarcodeDto>.Error("UNAUTHORIZED", "Token không chứa định danh người dùng hợp lệ.");

        var barcode = request?.NewBarcode?.Trim();
        var reason = request?.Reason?.Trim();
        if (string.IsNullOrEmpty(barcode) || barcode.Length > 50 || barcode.Any(char.IsControl) ||
            string.IsNullOrEmpty(reason) || reason.Length < 5 || reason.Length > 500 || reason.Any(char.IsControl))
            return ApiResponseDto<ChangeBarcodeDto>.Error("BAD_REQUEST", "Mã mới phải dài 1–50 ký tự, lý do 5–500 ký tự và không chứa ký tự điều khiển.");

        var result = await repository.ChangeAsync(bookId, barcode);
        if (result.Success && result.Data is { } data)
        {
            // Repository returns success only after the database transaction commits.
            logger.LogInformation("SecurityAudit BarcodeChanged ActorUserId={ActorUserId} BookId={BookId} OldBarcode={OldBarcode} NewBarcode={NewBarcode} Reason={Reason} ChangedAtUtc={ChangedAtUtc} CorrelationId={CorrelationId}",
                actorUserId, data.BookId, data.OldBarcode, data.NewBarcode, reason, data.ChangedAtUtc, correlationId);
        }
        return result;
    }
}
