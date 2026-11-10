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
            try
            {
                logger.LogInformation("SecurityAudit BarcodeChanged ActorUserId={ActorUserId} BookId={BookId} OldBarcode={OldBarcode} NewBarcode={NewBarcode} Reason={Reason} ChangedAtUtc={ChangedAtUtc} CorrelationId={CorrelationId}",
                    actorUserId, data.BookId, data.OldBarcode, data.NewBarcode, reason, data.ChangedAtUtc, correlationId);
            }
            catch (Exception)
            {
                // A logging provider cannot undo the committed cutover. Surface delivery failure
                // to the operator and send the committed audit to an independent stderr sink.
                // Do not retry through the same failing ILogger or expose exception details.
                result.Message = "Đổi mã vạch thành công. Vui lòng in lại tem mới. Cảnh báo: kênh nhật ký chính gặp lỗi; báo quản trị viên kiểm tra nhật ký dự phòng.";
                try
                {
                    Console.Error.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Event = "BarcodeAuditDeliveryFailed", Committed = true,
                        ActorUserId = actorUserId, data.BookId, data.OldBarcode, data.NewBarcode,
                        Reason = reason, data.ChangedAtUtc, CorrelationId = correlationId
                    }));
                }
                catch (Exception)
                {
                    // Even when both sinks fail, the successful response visibly reports the
                    // audit gap. Never claim rollback or encourage a retry of a committed write.
                    result.Message = "Đổi mã vạch thành công. Vui lòng in lại tem mới. Cảnh báo: không ghi được nhật ký; báo quản trị viên đối soát thay đổi này.";
                }
            }
        }
        return result;
    }
}
