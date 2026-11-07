using System;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Hardware;
using BookKiosk.Kiosk.Controls;
using Microsoft.Extensions.Configuration;

namespace BookKiosk.Kiosk
{
    public partial class MainWindow : Window
    {
        private readonly NavigationService _navigationService;
        private readonly IdleTimerService _idleTimerService;
        private readonly IBarcodeScanner _barcodeScanner;
        private readonly IConfiguration? _configuration;

        private int _clickCount = 0;
        private DateTime _lastClickTime = DateTime.MinValue;

        // Barcode Buffer
        private readonly StringBuilder _barcodeBuffer = new StringBuilder();
        private readonly DispatcherTimer _barcodeTimer;

        // Constructor rỗng dành cho WPF Designer
        public MainWindow()
        {
            InitializeComponent();
        }

        // Constructor tiêm Dependency Injection
        public MainWindow(NavigationService navigationService, IdleTimerService idleTimerService, IBarcodeScanner barcodeScanner, IDialogService dialogService, IConfiguration? configuration = null)
        {
            InitializeComponent();
            
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _idleTimerService = idleTimerService ?? throw new ArgumentNullException(nameof(idleTimerService));
            _barcodeScanner = barcodeScanner ?? throw new ArgumentNullException(nameof(barcodeScanner));
            _configuration = configuration;
            
            dialogService.RegisterDialogHandler(OverlayDialog.ShowDialogAsync);

            // Timer để reset buffer nếu gõ phím quá chậm (giả định khoảng cách giữa các phím của máy quét thật là < 50ms)
            _barcodeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _barcodeTimer.Tick += BarcodeTimer_Tick;

            // Khởi tạo Frame cho Navigation và bắt đầu đếm giờ
            _navigationService.Initialize(MainFrame);
            _idleTimerService.Start();

#if DEBUG
            // Khi dev: Cho phép Alt+Tab và xem các cửa sổ khác bình thường
            Topmost = false;
#else
            // Khi Release ở Kiosk thực tế: Ghim cửa sổ lớp trên cùng để chống thoát
            Topmost = true;
#endif
        }

        private void Window_PreviewInteraction(object sender, InputEventArgs e)
        {
            // Bất kỳ thao tác chuột, cảm ứng nào cũng sẽ reset bộ đếm idle
            _idleTimerService?.ResetTimer();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Reset Idle timer khi có thao tác phím (bao gồm quét mã)
            _idleTimerService?.ResetTimer();

#if DEBUG
            // DEV-ONLY: Bật/Tắt ô nhập Barcode thủ công bằng phím F2
            if (e.Key == Key.F2)
            {
                if (DevBarcodeBorder.Visibility == Visibility.Visible)
                {
                    DevBarcodeBorder.Visibility = Visibility.Collapsed;
                    MainFrame.Focus(); // Trả lại focus cho khung chính
                }
                else
                {
                    DevBarcodeBorder.Visibility = Visibility.Visible;
                    DevBarcodeTextBox.Focus();
                }
                e.Handled = true;
                return;
            }

            // Nếu TextBox dev đang mở và có focus, bỏ qua xử lý buffer tự động để gõ tay bình thường
            if (DevBarcodeTextBox.IsFocused) return;
#endif

            // Xử lý gom phím (Buffer) từ Barcode Scanner
            _barcodeTimer.Stop(); // Tạm dừng timer
            
            if (e.Key == Key.Enter)
            {
                // Khi máy quét gửi phím Enter -> kết thúc chuỗi mã
                string barcode = _barcodeBuffer.ToString();
                _barcodeBuffer.Clear();
                
                if (!string.IsNullOrEmpty(barcode))
                {
                    // Chuyển mã vạch sang Service qua cổng chung IKeyboardWedgeBarcodeSink
                    if (_barcodeScanner is IKeyboardWedgeBarcodeSink sink)
                    {
                        sink.ProcessKeyboardWedgeInput(barcode);
                    }
                    else if (_barcodeScanner is MockBarcodeScanner mock)
                    {
                        mock.SimulateScan(barcode);
                    }
                    e.Handled = true;
                }
            }
            else
            {
                // Gom ký tự (Cách này khá cơ bản, trong thực tế có thể dùng InputManager chuẩn hơn)
                string keyStr = e.Key.ToString();
                if (keyStr.Length == 1 || (keyStr.StartsWith("D") && keyStr.Length == 2) || (keyStr.StartsWith("NumPad") && keyStr.Length == 7))
                {
                    string charToAppend = keyStr.Replace("D", "").Replace("NumPad", "");
                    _barcodeBuffer.Append(charToAppend);
                }
                
                _barcodeTimer.Start(); // Bắt đầu đếm 50ms cho phím tiếp theo
            }
        }

        private void BarcodeTimer_Tick(object sender, EventArgs e)
        {
            // Nếu quá 50ms không có phím nào được bấm, đây có thể là người dùng gõ phím vật lý chậm chạp
            // Dọn dẹp buffer để tránh rác
            _barcodeTimer.Stop();
            _barcodeBuffer.Clear();
        }

        private void DevBarcodeTextBox_KeyDown(object sender, KeyEventArgs e)
        {
#if DEBUG
            if (e.Key == Key.Enter)
            {
                string barcode = DevBarcodeTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(barcode))
                {
                    // Đẩy barcode y hệt như máy quét thật qua cổng chung
                    if (_barcodeScanner is IKeyboardWedgeBarcodeSink sink)
                    {
                        sink.ProcessKeyboardWedgeInput(barcode);
                    }
                    else if (_barcodeScanner is MockBarcodeScanner mock)
                    {
                        mock.SimulateScan(barcode);
                    }
                    DevBarcodeTextBox.Clear();
                    
                    // Giấu đi sau khi nhập xong
                    DevBarcodeBorder.Visibility = Visibility.Collapsed;
                    MainFrame.Focus();
                }
                e.Handled = true;
            }
#endif
        }

        private void DevExitButton_Click(object sender, RoutedEventArgs e)
        {
            var now = DateTime.Now;
            
            // Reset bộ đếm nếu 2 lần click cách nhau quá 2 giây
            if ((now - _lastClickTime).TotalSeconds > 2)
            {
                _clickCount = 0;
            }

            _clickCount++;
            _lastClickTime = now;

            // Đủ 2 lần click liên tiếp -> Hiển thị Popup Mật khẩu bảo trì
            if (_clickCount >= 2)
            {
                _clickCount = 0; // Reset
                
#if DEBUG
                // [MAINT-02] Debug: Đọc PIN từ config ngoài source (env var hoặc appsettings.json/Development).
                // Mặc định dev fallback "123456" khi chạy local DEBUG
                string? configuredPin = Environment.GetEnvironmentVariable("KIOSK_MAINTENANCE_PIN")
                    ?? _configuration?["KioskSettings:MaintenancePin"]
                    ?? "123456";

                if (string.IsNullOrWhiteSpace(configuredPin))
                {
                    System.Diagnostics.Debug.WriteLine("[Security][DEBUG] Chức năng bảo trì bị vô hiệu hóa: Chưa cấu hình MaintenancePin ngoài source (Fail-closed).");
                    MessageBox.Show(
                        "[DEBUG] Chức năng bảo trì bị vô hiệu hóa.\n" +
                        "Vui lòng cấu hình KIOSK_MAINTENANCE_PIN (env) hoặc KioskSettings:MaintenancePin (appsettings.Development.json) ngoài source code.",
                        "Bảo mật — Fail Closed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var passwordDialog = new PasswordDialog(configuredPin)
                {
                    Owner = this
                };

                // Mở hộp thoại modal. Chỉ tắt app nếu nhập đúng pass
                if (passwordDialog.ShowDialog() == true && passwordDialog.IsAuthenticated)
                {
                    System.Windows.Application.Current.Shutdown();
                }
#else
                // [MAINT-01] Release: Luôn fail closed — không cho phép thoát Kiosk bằng local PIN.
                // Backend authorization chưa được triển khai → vô hiệu hóa hoàn toàn.
                System.Diagnostics.Debug.WriteLine("[Security][RELEASE] Chức năng thoát bảo trì bị vô hiệu hóa hoàn toàn ở Release (Fail-closed).");
                MessageBox.Show(
                    "Chức năng bảo trì không khả dụng trên thiết bị này.\nVui lòng liên hệ quản trị viên.",
                    "Bảo mật", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
#endif
            }
        }
    }
}