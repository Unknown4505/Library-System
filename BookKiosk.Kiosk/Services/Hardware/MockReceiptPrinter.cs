using System.Diagnostics;

namespace BookKiosk.Kiosk.Services.Hardware;

public class MockReceiptPrinter : IReceiptPrinter
{
    public async Task<bool> PrintReceiptAsync(string orderCode, decimal totalAmount)
    {
        // Giả lập độ trễ của máy in cơ học thật (1.5 giây)
        await Task.Delay(1500);

        Debug.WriteLine("========================================");
        Debug.WriteLine("[Mock Printer] NHÀ SÁCH TRÍ TUỆ - HÓA ĐƠN BÁN HÀNG");
        Debug.WriteLine($"[Mock Printer] MÃ ĐƠN  : {orderCode}");
        Debug.WriteLine($"[Mock Printer] TỔNG TIỀN : {totalAmount:N0} VNĐ");
        Debug.WriteLine("[Mock Printer] (In hoàn tất thành công)");
        Debug.WriteLine("========================================");
        
        return true;
    }
}
