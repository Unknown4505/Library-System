using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Kiosk.Controls;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Api;

namespace BookKiosk.Kiosk.ViewModels
{
    /// <summary>
    /// Các trạng thái UI của màn hình hóa đơn Kiosk.
    /// </summary>
    public enum ReceiptState
    {
        Loading,
        Success,
        Error
    }

    /// <summary>
    /// Phân loại lỗi khi gọi API hóa đơn.
    /// </summary>
    public enum ReceiptErrorKind
    {
        None,
        NotFound,
        Conflict,
        NetworkOrTimeout,
        ServerError
    }

    /// <summary>
    /// ViewModel quản lý màn hình Hóa đơn (Task 8.2.1, Issue #29).
    /// Tuân thủ quy chuẩn Server-owned ReceiptDto, State Machine, chống re-entrancy và Timer an toàn.
    /// </summary>
    public class ReceiptViewModel : BaseViewModel
    {
        private readonly IBookKioskApiClient _apiClient;
        private readonly CartService _cartService;
        private readonly NavigationService _navigationService;
        private readonly IReceiptService _receiptService;

        private DispatcherTimer? _timer;
        private MockReceiptDialog? _mockDialog;
        private CancellationTokenSource? _cts;

        private int _orderId;
        private string _orderCode = string.Empty;
        private int _retryCount;
        private bool _isProcessing;
        private bool _isPrinting;
        private bool _hasPrinted;

        private ReceiptState _currentState = ReceiptState.Loading;
        public ReceiptState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    _currentState = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsStateLoading));
                    OnPropertyChanged(nameof(IsStateSuccess));
                    OnPropertyChanged(nameof(IsStateError));
                }
            }
        }

        public bool IsStateLoading => CurrentState == ReceiptState.Loading;
        public bool IsStateSuccess => CurrentState == ReceiptState.Success;
        public bool IsStateError => CurrentState == ReceiptState.Error;

        private ReceiptErrorKind _errorKind = ReceiptErrorKind.None;
        public ReceiptErrorKind ErrorKind
        {
            get => _errorKind;
            private set
            {
                if (_errorKind != value)
                {
                    _errorKind = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _canRetry;
        public bool CanRetry
        {
            get => _canRetry;
            private set
            {
                if (_canRetry != value)
                {
                    _canRetry = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _autoExitCountdown = 5;
        public int AutoExitCountdown
        {
            get => _autoExitCountdown;
            private set
            {
                if (_autoExitCountdown != value)
                {
                    _autoExitCountdown = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CountdownText));
                }
            }
        }

        public string CountdownText => $"Tự quay về màn hình chính sau {AutoExitCountdown}s";

        private ReceiptDto? _receipt;
        public ReceiptDto? Receipt
        {
            get => _receipt;
            private set
            {
                if (_receipt != value)
                {
                    _receipt = value;
                    OnPropertyChanged();
                }
            }
        }

        public string OrderCode => _orderCode;

        public ICommand RetryCommand { get; }
        public ICommand GoHomeCommand { get; }

        public ReceiptViewModel(
            IBookKioskApiClient apiClient,
            CartService cartService,
            NavigationService navigationService,
            IReceiptService receiptService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _cartService = cartService ?? throw new ArgumentNullException(nameof(cartService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _receiptService = receiptService ?? throw new ArgumentNullException(nameof(receiptService));

            RetryCommand = new RelayCommand(_ => ExecuteRetry(), _ => CanRetry && !IsLoading);
            GoHomeCommand = new RelayCommand(_ => ExecuteGoHome());
        }

        /// <summary>
        /// Khởi tạo nhận tham số điều hướng từ Checkout (mang OrderId và OrderCode thật).
        /// </summary>
        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);

            _orderId = 0;
            _orderCode = string.Empty;
            _retryCount = 0;
            _hasPrinted = false;
            _isPrinting = false;

            if (parameter is ReceiptParameter rp)
            {
                _orderId = rp.OrderId;
                _orderCode = rp.OrderCode;
            }
            else if (parameter is int id)
            {
                _orderId = id;
                _orderCode = id.ToString();
            }

            OnPropertyChanged(nameof(OrderCode));

            if (_orderId <= 0)
            {
                CurrentState = ReceiptState.Error;
                ErrorKind = ReceiptErrorKind.NotFound;
                ErrorMessage = "Không tìm thấy mã đơn hàng hợp lệ. Vui lòng liên hệ nhân viên.";
                CanRetry = false;
                return;
            }

            _ = FetchReceiptAsync();
        }

        /// <summary>
        /// Gọi API lấy hóa đơn chính thức từ server kèm quản lý CancellationToken và State Machine.
        /// </summary>
        public async Task FetchReceiptAsync()
        {
            if (_isProcessing)
            {
                return;
            }

            _isProcessing = true;
            IsLoading = true;
            CurrentState = ReceiptState.Loading;
            ErrorMessage = string.Empty;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                var response = await _apiClient.GetReceiptAsync(_orderId, _cts.Token);

                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                if (response != null && response.Success && response.Data != null)
                {
                    Receipt = response.Data;
                    _orderCode = response.Data.OrderCode;
                    OnPropertyChanged(nameof(OrderCode));

                    CurrentState = ReceiptState.Success;
                    ErrorMessage = string.Empty;
                    CanRetry = false;

                    // Chỉ mở Mock Dialog và xuất PDF khi Success
                    await ProcessPrintAndStartTimerAsync(response.Data);
                }
                else
                {
                    HandleErrorResponse(response?.Code, response?.Message);
                }
            }
            catch (OperationCanceledException)
            {
                // Bị hủy khi người dùng rời trang
            }
            catch (Exception ex)
            {
                HandleErrorResponse("UNKNOWN_ERROR", ex.Message);
            }
            finally
            {
                _isProcessing = false;
                IsLoading = false;
            }
        }

        private void HandleErrorResponse(string? code, string? message)
        {
            CurrentState = ReceiptState.Error;

            switch (code)
            {
                case "NOT_FOUND":
                    ErrorKind = ReceiptErrorKind.NotFound;
                    ErrorMessage = $"Vui lòng liên hệ nhân viên tại quầy để nhận hóa đơn (Mã đơn: #{_orderCode})";
                    CanRetry = false;
                    break;

                case "CONFLICT":
                    ErrorKind = ReceiptErrorKind.Conflict;
                    ErrorMessage = $"Vui lòng liên hệ nhân viên tại quầy để nhận hóa đơn (Mã đơn: #{_orderCode})";
                    CanRetry = false;
                    break;

                case "TIMEOUT_ERROR":
                case "NETWORK_ERROR":
                    ErrorKind = ReceiptErrorKind.NetworkOrTimeout;
                    EvaluateRetryLimit("Lỗi kết nối máy chủ khi lấy hóa đơn. Vui lòng bấm Thử lại.");
                    break;

                default:
                    ErrorKind = ReceiptErrorKind.ServerError;
                    EvaluateRetryLimit("Có lỗi khi lấy hóa đơn từ máy chủ. Vui lòng thử lại hoặc liên hệ nhân viên.");
                    break;
            }
        }

        private void EvaluateRetryLimit(string baseMessage)
        {
            _retryCount++;
            if (_retryCount >= 3)
            {
                CanRetry = false;
                ErrorMessage = $"Đã thử lại 3 lần không thành công. Vui lòng liên hệ nhân viên tại quầy để nhận hóa đơn (Mã đơn: #{_orderCode})";
            }
            else
            {
                CanRetry = true;
                ErrorMessage = $"{baseMessage} (Lần {_retryCount}/3)";
            }
        }

        private void ExecuteRetry()
        {
            if (CanRetry && !IsLoading)
            {
                _ = FetchReceiptAsync();
            }
        }

        private void ExecuteGoHome()
        {
            StopTimer();
            CloseMockDialog();

            // Đơn hàng đã Paid nên việc quay về trang chủ chủ động bởi khách sẽ xóa giỏ hàng
            _cartService.ClearCart();
            _navigationService.Navigate<IdlePage>();
        }

        /// <summary>
        /// Mở Mock Printer và xuất PDF từ dữ liệu ReceiptDto (đúng 1 lần duy nhất cho mỗi đơn).
        /// Sau khi hoàn tất sẽ kích hoạt Timer 5 giây đếm ngược quay về IdlePage.
        /// </summary>
        private async Task ProcessPrintAndStartTimerAsync(ReceiptDto receiptDto)
        {
            if (_hasPrinted || _isPrinting)
            {
                return;
            }

            _isPrinting = true;

            try
            {
                // 1. Mở cửa sổ giả lập in hóa đơn nhiệt
                try
                {
                    CloseMockDialog();
                    _mockDialog = new MockReceiptDialog(receiptDto);
                    _mockDialog.Show();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ReceiptViewModel] Không thể mở MockReceiptDialog: {ex.Message}");
                }

                // 2. Xuất file PDF hóa đơn chính thức
                var pdfPath = await _receiptService.GenerateReceiptPdfAsync(receiptDto);
                try
                {
                    Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ReceiptViewModel] Lỗi mở trình đọc PDF: {ex.Message}");
                }

                _hasPrinted = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReceiptViewModel] Lỗi xử lý in hóa đơn: {ex.Message}");
            }
            finally
            {
                _isPrinting = false;
            }

            // 3. Khởi tạo Timer 5 giây CHỈ SAU KHI hóa đơn đã hiển thị & in/PDF xong
            StartAutoExitTimer();
        }

        private void StartAutoExitTimer()
        {
            StopTimer();

            AutoExitCountdown = 5;
            try
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReceiptViewModel] Không thể khởi động DispatcherTimer: {ex.Message}");
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            AutoExitCountdown--;

            if (AutoExitCountdown <= 0)
            {
                StopTimer();
                CloseMockDialog();

                // Điều hướng về IdlePage trước
                _navigationService.Navigate<IdlePage>();

                // Dọn giỏ hàng sau khi đã hoàn tất toàn bộ luồng hóa đơn thành công
                _cartService.ClearCart();
            }
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer = null;
            }
        }

        private void CloseMockDialog()
        {
            if (_mockDialog != null)
            {
                try
                {
                    _mockDialog.Close();
                }
                catch
                {
                    // Tránh crash nếu dialog đã bị đóng bởi OS
                }
                _mockDialog = null;
            }
        }

        /// <summary>
        /// Dọn dẹp tài nguyên khi rời trang (hủy request ngầm, hủy timer, đóng dialog).
        /// </summary>
        public void Cleanup()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            StopTimer();
            CloseMockDialog();
        }
    }
}
