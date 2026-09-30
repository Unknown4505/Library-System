using System.Diagnostics;

namespace BookKiosk.Kiosk.Services.Hardware;

public class MockReceiptPrinter : IReceiptPrinter
{
    public async Task<bool> PrintReceiptAsync(string orderCode, decimal totalAmount)
    {
        // Giả lập độ trễ của máy in cơ học thật (1.5 giây)
        await Task.Delay(1500);

        // TODO: @lehuukhang (Khang) - Bạn tự viết logic tạo Mock Data in hóa đơn (Debug.WriteLine) ở đây nhé.
        // Việc tự viết sẽ giúp bạn nắm rõ các trường dữ liệu cần in ra UI sau này.
        // Gợi ý: In ra Mã đơn (orderCode) và Tổng tiền (totalAmount).
        
        return true;
    }
}
