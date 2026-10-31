using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using ZXing;
using ZXing.Common;

namespace BookKiosk.Kiosk.Services.Hardware;

/// <summary>
/// Triển khai thực tế của IBarcodeScanner cho môi trường Production / Kiosk.
/// Tiếp nhận mã từ máy quét USB (Keyboard-Wedge) và Camera live frame.
/// Áp dụng cơ chế Deduplication thông minh:
/// - Chống nảy phím USB (ngưỡng ngắn UsbBounceDebounceMs = 250ms &lt;= 300ms).
/// - Khử trùng liên tục từ Camera khi camera liên tục nhìn thấy cùng một mã trên nhiều frame liên tiếp.
/// - Cho phép quét lại cùng mã khi người dùng quét có chủ ý (sau khi camera không còn thấy mã hoặc sau khi xử lý xong và qua ngưỡng nảy phím).
/// - USB keyboard-wedge vẫn hoạt động an toàn ngay cả khi camera không mở được hoặc không có camera trên thiết bị.
/// - Tự động phát âm thanh Beep khi nhận diện mã thành công.
/// </summary>
public class KioskBarcodeScanner : IBarcodeScanner, IKeyboardWedgeBarcodeSink, ICameraFrameSource, IDisposable
{
    public event EventHandler<string>? BarcodeScanned;
    public event EventHandler<string>? ScannerErrorOccurred;
    public event EventHandler<BitmapSource>? FrameReady;

    // Hằng số nảy phím USB (<= 300ms theo đặc tả yêu cầu)
    public const int UsbBounceDebounceMs = 250;
    public const int MaxBarcodeLength = 128;
    public const int MaxDecodeFps = 10;

    private readonly object _lock = new object();
    private bool _isListening = false;
    private bool _isCameraAvailable = true;

    private string? _lastDispatchedBarcode = null;
    private long _lastDispatchedTimestamp = 0;
    private bool _isProcessing = false;
    private string? _continuousCameraBarcode = null;

    private CancellationTokenSource? _cameraCts;
    private Task? _cameraTask;

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
            ScannerErrorOccurred?.Invoke(this, "Không thể mở camera laptop (thiết bị có thể bị chiếm giữ hoặc thiếu quyền). Máy quét mã USB vẫn hoạt động.");
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

    /// <summary>
    /// Mở camera laptop và khởi động capture loop.
    /// </summary>
    protected virtual void TryInitializeCamera()
    {
        TryStopCamera();

        VideoCapture? capture = CreateVideoCapture();
        if (capture == null || !capture.IsOpened())
        {
            capture?.Dispose();
            lock (_lock)
            {
                _isCameraAvailable = false;
            }
            ScannerErrorOccurred?.Invoke(this, "Không thể mở camera laptop (thiết bị có thể bị chiếm giữ hoặc thiếu quyền). Máy quét mã USB vẫn hoạt động.");
            return;
        }

        lock (_lock)
        {
            _isCameraAvailable = true;
            _cameraCts = new CancellationTokenSource();
            var token = _cameraCts.Token;
            _cameraTask = Task.Run(() => CaptureLoop(capture, token), token);
        }
    }

    /// <summary>
    /// Factory method mở camera, hỗ trợ override trong môi trường kiểm thử.
    /// </summary>
    protected virtual VideoCapture? CreateVideoCapture()
    {
        try
        {
            var capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            if (capture.IsOpened())
            {
                try
                {
                    capture.Set(VideoCaptureProperties.FrameWidth, 640);
                    capture.Set(VideoCaptureProperties.FrameHeight, 480);
                }
                catch { }
                return capture;
            }
            capture.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Kiosk Scanner] Exception khi mở camera: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Dừng camera loop an toàn, giải phóng capture task và token.
    /// </summary>
    protected virtual void TryStopCamera()
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_lock)
        {
            cts = _cameraCts;
            task = _cameraTask;
            _cameraCts = null;
            _cameraTask = null;
        }

        if (cts != null)
        {
            try
            {
                cts.Cancel();
            }
            catch { }
        }

        if (task != null)
        {
            try
            {
                task.Wait(TimeSpan.FromSeconds(2));
            }
            catch { }
        }

        if (cts != null)
        {
            try
            {
                cts.Dispose();
            }
            catch { }
        }
    }

    /// <summary>
    /// Vòng lặp chụp frame liên tục từ camera, chuyển đổi preview và giải mã mã vạch/QR.
    /// </summary>
    private void CaptureLoop(VideoCapture capture, CancellationToken ct)
    {
        using (capture)
        using (var frame = new Mat())
        {
            var stopwatch = Stopwatch.StartNew();
            const int minIntervalMs = 1000 / MaxDecodeFps;

            while (!ct.IsCancellationRequested)
            {
                long frameStart = stopwatch.ElapsedMilliseconds;

                bool readSuccess = false;
                try
                {
                    readSuccess = capture.Read(frame);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Kiosk Scanner] Ngoại lệ khi đọc frame: {ex.Message}");
                    break;
                }

                if (!readSuccess || frame.Empty())
                {
                    NotifyCameraFrameNoBarcode();
                    try { Task.Delay(50, ct).Wait(ct); } catch (OperationCanceledException) { break; }
                    continue;
                }

                // 1. Đưa frame lên sự kiện preview
                try
                {
                    var previewBitmap = ConvertMatToBitmapSource(frame);
                    if (previewBitmap != null)
                    {
                        FrameReady?.Invoke(this, previewBitmap);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Kiosk Scanner] Lỗi convert preview: {ex.Message}");
                }

                // 2. Giải mã mã vạch / QR
                try
                {
                    string? decoded = DecodeMat(frame);
                    if (!string.IsNullOrWhiteSpace(decoded))
                    {
                        ProcessCameraBarcode(decoded);
                    }
                    else
                    {
                        NotifyCameraFrameNoBarcode();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Kiosk Scanner] Lỗi ZXing decode: {ex.Message}");
                    NotifyCameraFrameNoBarcode();
                }

                // 3. Giới hạn tốc độ khung hình (10 FPS)
                long elapsed = stopwatch.ElapsedMilliseconds - frameStart;
                int delay = minIntervalMs - (int)elapsed;
                if (delay > 0)
                {
                    try
                    {
                        Task.Delay(delay, ct).Wait(ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Chuyển đổi OpenCvSharp Mat sang WPF BitmapSource và Freeze để chia sẻ an toàn xuyên thread.
    /// </summary>
    public static BitmapSource? ConvertMatToBitmapSource(Mat mat)
    {
        if (mat.Empty()) return null;

        int width = mat.Width;
        int height = mat.Height;
        int stride = (int)mat.Step();
        var format = System.Windows.Media.PixelFormats.Bgr24;

        if (mat.Channels() == 1)
            format = System.Windows.Media.PixelFormats.Gray8;
        else if (mat.Channels() == 4)
            format = System.Windows.Media.PixelFormats.Bgra32;

        var bs = BitmapSource.Create(
            width,
            height,
            96,
            96,
            format,
            null,
            mat.Data,
            stride * height,
            stride);

        bs.Freeze();
        return bs;
    }

    /// <summary>
    /// Giải mã mã vạch / QR từ Mat hình ảnh bằng ZXing.Net.
    /// Hỗ trợ EAN-13, EAN-8, Code 128 và QR Code.
    /// </summary>
    public static string? DecodeMat(Mat frame)
    {
        if (frame.Empty()) return null;

        int width = frame.Width;
        int height = frame.Height;
        int step = (int)frame.Step();
        int totalBytes = step * height;

        byte[] pixelBuffer = new byte[totalBytes];
        System.Runtime.InteropServices.Marshal.Copy(frame.Data, pixelBuffer, 0, totalBytes);

        var format = ZXing.RGBLuminanceSource.BitmapFormat.BGR24;
        if (frame.Channels() == 1)
            format = ZXing.RGBLuminanceSource.BitmapFormat.Gray8;
        else if (frame.Channels() == 4)
            format = ZXing.RGBLuminanceSource.BitmapFormat.BGRA32;

        var luminanceSource = new ZXing.RGBLuminanceSource(
            pixelBuffer,
            width,
            height,
            format);

        var reader = new ZXing.BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<ZXing.BarcodeFormat>
                {
                    ZXing.BarcodeFormat.EAN_13,
                    ZXing.BarcodeFormat.EAN_8,
                    ZXing.BarcodeFormat.CODE_128,
                    ZXing.BarcodeFormat.QR_CODE
                }
            }
        };

        var result = reader.Decode(luminanceSource);
        return result?.Text?.Trim();
    }

    public void Dispose()
    {
        StopListening();
        BarcodeScanned = null;
        ScannerErrorOccurred = null;
        FrameReady = null;
    }
}
