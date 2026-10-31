using System;
using System.Diagnostics;

namespace BookKiosk.Kiosk.Services.Hardware;

/// <summary>
/// Triển khai thực tế của IBarcodeScanner cho môi trường Production / Kiosk.
/// Tiếp nhận mã từ máy quét USB (Keyboard-Wedge) và Camera live frame.
/// Áp dụng cơ chế Deduplication thông minh:
/// - Chống nảy phím USB (ngưỡng ngắn UsbBounceDebounceMs = 250ms <= 300ms).
/// - Khử trùng liên tục từ Camera khi camera liên tục nhìn thấy cùng một mã trên nhiều frame liên tiếp.
/// - Cho phép quét lại cùng mã khi người dùng quét có chủ ý (sau khi camera không còn thấy mã hoặc sau khi xử lý xong và qua ngưỡng nảy phím).
/// - USB keyboard-wedge vẫn hoạt động an toàn ngay cả khi camera không mở được hoặc không có camera trên thiết bị.
/// </summary>
public class KioskBarcodeScanner : IBarcodeScanner, IKeyboardWedgeBarcodeSink, IDisposable
{
    public event EventHandler<string>? BarcodeScanned;
    public event EventHandler<string>? ScannerErrorOccurred;

    // Hằng số nảy phím USB (<= 300ms theo đặc tả yêu cầu)
    public const int UsbBounceDebounceMs = 250;
    public const int MaxBarcodeLength = 128;

    private readonly object _lock = new object();
    private bool _isListening = false;
    private bool _isCameraAvailable = true;

    private string? _lastDispatchedBarcode = null;
    private long _lastDispatchedTimestamp = 0;
    private bool _isProcessing = false;
    private string? _continuousCameraBarcode = null;

    public bool IsListening
    {
        get
        {
            lock (_lock)
            {
                return _isListening;
            }
        }
    }

    public bool IsCameraAvailable
    {
        get
        {
            lock (_lock)
            {
                return _isCameraAvailable;
            }
        }
    }

    public void StartListening()
    {
        lock (_lock)
        {
            if (_isListening) return;
            _isListening = true;
        }

        // Khởi tạo / mở camera nếu hỗ trợ; nếu camera lỗi hoặc không có thiết bị,
        // ghi nhận lỗi nhưng KHÔNG ném ngoại lệ và KHÔNG chuyển sang Mock.
        // Cờ _isListening vẫn bật để USB Wedge tiếp tục hoạt động độc lập.
        try
        {
            TryInitializeCamera();
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _isCameraAvailable = false;
            }
            Debug.WriteLine($"[Kiosk Scanner] Không thể khởi tạo camera: {ex.Message}. USB Wedge vẫn hoạt động bình thường.");
            ScannerErrorOccurred?.Invoke(this, "Không thể mở camera (thiết bị có thể bị chiếm giữ hoặc thiếu quyền). Máy quét mã USB vẫn hoạt động.");
        }

        Debug.WriteLine("[Kiosk Scanner] Đã bật chế độ lắng nghe mã vạch (USB Wedge + Camera).");
    }

    public void StopListening()
    {
        lock (_lock)
        {
            if (!_isListening) return;
            _isListening = false;
            _continuousCameraBarcode = null;
            _isProcessing = false;
        }

        try
        {
            TryStopCamera();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Kiosk Scanner] Lỗi khi dừng camera: {ex.Message}");
        }

        Debug.WriteLine("[Kiosk Scanner] Đã tắt chế độ lắng nghe mã vạch.");
    }

    /// <summary>
    /// Tiếp nhận chuỗi mã từ luồng USB Keyboard-Wedge.
    /// </summary>
    public void ProcessKeyboardWedgeInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        string cleanBarcode = input.Trim();
        if (cleanBarcode.Length == 0 || cleanBarcode.Length > MaxBarcodeLength)
        {
            Debug.WriteLine($"[Kiosk Scanner] Bỏ qua mã USB không hợp lệ (độ dài: {cleanBarcode.Length}).");
            return;
        }

        lock (_lock)
        {
            if (!_isListening)
            {
                Debug.WriteLine($"[Kiosk Scanner] Bỏ qua mã USB '{cleanBarcode}' vì scanner đang tắt (StopListening).");
                return;
            }

            // 1. Nếu mã trước còn đang xử lý và trùng mã -> bỏ qua
            if (_isProcessing && string.Equals(_lastDispatchedBarcode, cleanBarcode, StringComparison.Ordinal))
            {
                Debug.WriteLine($"[Kiosk Scanner] Bỏ qua mã USB '{cleanBarcode}' vì yêu cầu trước đang được xử lý.");
                return;
            }

            // 2. Chống nảy phím USB: cùng mã và khoảng thời gian < UsbBounceDebounceMs -> bỏ qua
            long now = Stopwatch.GetTimestamp();
            if (_lastDispatchedTimestamp > 0 && string.Equals(_lastDispatchedBarcode, cleanBarcode, StringComparison.Ordinal))
            {
                double elapsedMs = (now - _lastDispatchedTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < UsbBounceDebounceMs)
                {
                    Debug.WriteLine($"[Kiosk Scanner] Dedupe USB: Bỏ qua nảy phím trùng '{cleanBarcode}' ({elapsedMs:F1}ms < {UsbBounceDebounceMs}ms).");
                    return;
                }
            }

            _lastDispatchedBarcode = cleanBarcode;
            _lastDispatchedTimestamp = now;
            _isProcessing = true;
        }

        try
        {
            Debug.WriteLine($"[Kiosk Scanner] Tít! Phát sự kiện mã từ USB: {cleanBarcode}");
            try { System.Media.SystemSounds.Beep.Play(); } catch { }
            BarcodeScanned?.Invoke(this, cleanBarcode);
        }
        finally
        {
            lock (_lock)
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>
    /// Tiếp nhận mã từ khung hình giải mã của Camera.
    /// Khử trùng liên tục: frame sau thấy cùng mã không phát lại cho tới khi camera thấy khung hình không có mã hoặc mã khác.
    /// </summary>
    public void ProcessCameraBarcode(string? barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;

        string cleanBarcode = barcode.Trim();
        if (cleanBarcode.Length == 0 || cleanBarcode.Length > MaxBarcodeLength) return;

        lock (_lock)
        {
            if (!_isListening) return;

            // Nếu camera đang thấy liên tục cùng một mã trên các frame kế tiếp -> bỏ qua
            if (string.Equals(_continuousCameraBarcode, cleanBarcode, StringComparison.Ordinal))
            {
                return;
            }

            // Nếu mã trước đang xử lý và trùng mã -> bỏ qua
            if (_isProcessing && string.Equals(_lastDispatchedBarcode, cleanBarcode, StringComparison.Ordinal))
            {
                return;
            }

            // Chống xung đột giữa USB và Camera: nếu vừa quét từ USB/Camera cùng mã < UsbBounceDebounceMs -> bỏ qua
            long now = Stopwatch.GetTimestamp();
            if (_lastDispatchedTimestamp > 0 && string.Equals(_lastDispatchedBarcode, cleanBarcode, StringComparison.Ordinal))
            {
                double elapsedMs = (now - _lastDispatchedTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < UsbBounceDebounceMs)
                {
                    return;
                }
            }

            _continuousCameraBarcode = cleanBarcode;
            _lastDispatchedBarcode = cleanBarcode;
            _lastDispatchedTimestamp = now;
            _isProcessing = true;
        }

        try
        {
            Debug.WriteLine($"[Kiosk Scanner] Tít! Phát sự kiện mã từ Camera: {cleanBarcode}");
            try { System.Media.SystemSounds.Beep.Play(); } catch { }
            BarcodeScanned?.Invoke(this, cleanBarcode);
        }
        finally
        {
            lock (_lock)
            {
                _isProcessing = false;
            }
        }
    }

    /// <summary>
    /// Gọi từ frame loop của Camera khi khung hình không phát hiện được mã nào.
    /// Giúp reset trạng thái khử trùng liên tục để người dùng đưa sách lại là quét được ngay.
    /// </summary>
    public void NotifyCameraFrameNoBarcode()
    {
        lock (_lock)
        {
            _continuousCameraBarcode = null;
        }
    }

    /// <summary>
    /// Cho phép thiết lập thủ công trạng thái đang xử lý (dùng trong test hoặc khi gọi API tra cứu).
    /// </summary>
    public void SetProcessingState(bool isProcessing)
    {
        lock (_lock)
        {
            _isProcessing = isProcessing;
        }
    }

    // Các hàm móc nối camera (sẽ kích hoạt thư viện native ở FEAT-01)
    protected virtual void TryInitializeCamera()
    {
        // Ở FEAT-03 chuẩn bị interface và lifecycle; FEAT-01 sẽ nạp VideoCapture thực tế.
    }

    protected virtual void TryStopCamera()
    {
        // Ở FEAT-03 chuẩn bị interface và lifecycle; FEAT-01 sẽ dừng capture loop và giải phóng Mat/VideoCapture.
    }

    public void Dispose()
    {
        StopListening();
        BarcodeScanned = null;
        ScannerErrorOccurred = null;
    }
}
