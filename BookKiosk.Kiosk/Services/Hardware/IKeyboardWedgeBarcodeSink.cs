namespace BookKiosk.Kiosk.Services.Hardware;

/// <summary>
/// Cổng tiếp nhận chuỗi ký tự từ máy quét USB (hoạt động như bàn phím ảo Keyboard-Wedge).
/// Tách biệt khỏi IBarcodeScanner để tuân thủ quy tắc không sửa đổi interface của Leader.
/// Interface phụ phục vụ USB Keyboard Wedge — cần Leader xem xét và phê duyệt chính thức.
/// </summary>
public interface IKeyboardWedgeBarcodeSink
{
    void ProcessKeyboardWedgeInput(string barcode);
}
