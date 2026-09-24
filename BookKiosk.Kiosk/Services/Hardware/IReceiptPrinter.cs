namespace BookKiosk.Kiosk.Services.Hardware;

public interface IReceiptPrinter
{
    // Hàm in hóa đơn, trả về true nếu in thành công
    Task<bool> PrintReceiptAsync(string orderCode, decimal totalAmount);
}
