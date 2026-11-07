using System;
using System.Windows.Media.Imaging;

namespace BookKiosk.Kiosk.Services.Hardware;

/// <summary>
/// Nguồn cung cấp khung hình camera và trạng thái lỗi phần cứng cho giao diện Kiosk.
/// Tách biệt để không thay đổi chữ ký IBarcodeScanner của Leader.
/// </summary>
public interface ICameraFrameSource
{
    event EventHandler<BitmapSource>? FrameReady;
    event EventHandler<string>? ScannerErrorOccurred;
    bool IsCameraAvailable { get; }
}
