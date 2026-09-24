using System.Diagnostics;

namespace BookKiosk.Kiosk.Services.Hardware;

public class MockBarcodeScanner : IBarcodeScanner
{
    public event EventHandler<string>? BarcodeScanned;

    public void StartListening()
    {
        Debug.WriteLine("[Mock Scanner] Đã bật chế độ lắng nghe mã vạch.");
    }

    public void StopListening()
    {
        Debug.WriteLine("[Mock Scanner] Đã tắt chế độ lắng nghe mã vạch.");
    }

    /// <summary>
    /// Hàm này chỉ dùng cho Dev để giả lập việc quét mã vạch bằng cách 
    /// gọi hàm này từ nút bấm trên UI hoặc gõ phím trên Laptop.
    /// </summary>
    public void SimulateScan(string mockBarcode)
    {
        Debug.WriteLine($"[Mock Scanner] Tít! Mã vạch nhận được: {mockBarcode}");
        BarcodeScanned?.Invoke(this, mockBarcode);
    }
}
