using System;
using System.Diagnostics;
using System.IO;
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

        // Quản lý Lifecycle & State Machine tạo PDF (Issue #29 hardening)
        private bool _isCleanedUp;
        private int _currentRequestId;
        private bool _dialogShown;
        private bool _pdfCompleted;
        private int _pdfRetryCount;
        private bool _isPdfGenerationError;
        private string _pdfErrorMessage = string.Empty;
        private bool _canRetryPrint;

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
                    OnPropertyChanged(nameof(IsPdfNormalSuccess));
                    OnPropertyChanged(nameof(IsPdfCreating));
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

        public bool IsPdfGenerationError
        {
            get => _isPdfGenerationError;
            private set
            {
                if (_isPdfGenerationError != value)
                {
                    _isPdfGenerationError = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPdfNormalSuccess));
                    OnPropertyChanged(nameof(IsPdfCreating));
                }
            }
        }

        public bool IsPdfNormalSuccess => CurrentState == ReceiptState.Success && _pdfCompleted && !_isPdfGenerationError;
        public bool IsPdfCreating => CurrentState == ReceiptState.Success && !_pdfCompleted && !_isPdfGenerationError;

        private void RaisePdfStatePropertiesChanged()
        {
            OnPropertyChanged(nameof(IsPdfNormalSuccess));
            OnPropertyChanged(nameof(IsPdfCreating));
            OnPropertyChanged(nameof(IsPdfGenerationError));
            OnPropertyChanged(nameof(PdfErrorMessage));
            OnPropertyChanged(nameof(CanRetryPrint));
        }

        public string PdfErrorMessage
        {
            get => _pdfErrorMessage;
            private set
            {
                if (_pdfErrorMessage != value)
                {
                    _pdfErrorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanRetryPrint
        {
            get => _canRetryPrint;
            private set
            {
                if (_canRetryPrint != value)
                {
                    _canRetryPrint = value;
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
        public bool IsAutoExitTimerRunning => _timer != null && _timer.IsEnabled;

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
        public ICommand RetryPrintCommand { get; }
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
            RetryPrintCommand = new RelayCommand(_ => ExecuteRetryPrint(), _ => CanRetryPrint && !_isPrinting);
            GoHomeCommand = new RelayCommand(_ => ExecuteGoHome());
        }

        /// <summary>
        /// Khởi tạo nhận tham số điều hướng từ Checkout (mang OrderId và OrderCode thật).
        /// </summary>
        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);

            _isCleanedUp = false;
            _orderId = 0;
            _orderCode = string.Empty;
            _retryCount = 0;
            _pdfRetryCount = 0;
            _dialogShown = false;
            _pdfCompleted = false;
            _isPrinting = false;
            IsPdfGenerationError = false;
            PdfErrorMessage = string.Empty;
            CanRetryPrint = false;
            RaisePdfStatePropertiesChanged();

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
            if (_isProcessing || _isCleanedUp)
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

            // LỖI 2: Chụp token và generation vào biến cục bộ TRƯỚC await
            var token = _cts.Token;
            var requestId = ++_currentRequestId;

            try
            {
                var response = await _apiClient.GetReceiptAsync(_orderId, token);

                // Sau await: kiểm tra token và cờ cleanup, tuyệt đối không dùng _cts field
                if (token.IsCancellationRequested || _isCleanedUp || requestId != _currentRequestId)
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
                // Bị hủy khi người dùng rời trang -> nuốt im lặng
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested || _isCleanedUp || requestId != _currentRequestId)
                {
                    return;
                }
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
            _navigationService.Navigate<SearchPage>();
        }

        /// <summary>
        /// Mở Mock Printer và xuất PDF từ dữ liệu ReceiptDto (đúng 1 lần duy nhất cho mỗi đơn).
        /// Sau khi hoàn tất thành công sẽ kích hoạt Timer 5 giây đếm ngược quay về IdlePage.
        /// </summary>
        private async Task ProcessPrintAndStartTimerAsync(ReceiptDto receiptDto)
        {
            if (_pdfCompleted || _isPrinting || _isCleanedUp || _cts == null)
            {
                return;
            }

            var cts = _cts;
            if (cts == null || _isCleanedUp)
            {
                return;
            }

            CancellationToken token;
            try
            {
                token = cts.Token;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var requestId = _currentRequestId;

            _isPrinting = true;
            IsPdfGenerationError = false;
            PdfErrorMessage = string.Empty;
            RaisePdfStatePropertiesChanged();

            try
            {
                // 1. Mở cửa sổ giả lập in hóa đơn nhiệt (đúng 1 lần duy nhất cho mỗi đơn)
                if (!_dialogShown)
                {
                    try
                    {
                        CloseMockDialog();
                        _mockDialog = new MockReceiptDialog(receiptDto);
                        _mockDialog.Show();
                        _dialogShown = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ReceiptViewModel] Không thể mở MockReceiptDialog: {ex.Message}");
                    }
                }

                // Guard trước await
                if (_isCleanedUp || _cts == null || token.IsCancellationRequested || requestId != _currentRequestId)
                {
                    return;
                }

                // 2. Xuất file PDF hóa đơn chính thức
                var pdfPath = await _receiptService.GenerateReceiptPdfAsync(receiptDto);

                // GUARD SAU AWAIT (Nhánh SUCCESS): kiểm tra trước MỌI side-effect
                if (_isCleanedUp || _cts == null || token.IsCancellationRequested || requestId != _currentRequestId)
                {
                    return;
                }

                // LỖI 1: Kiểm tra tính hợp lệ của file PDF ngay trong ViewModel
                bool isValid = !string.IsNullOrWhiteSpace(pdfPath) &&
                               File.Exists(pdfPath) &&
                               new FileInfo(pdfPath).Length > 0;

                if (!isValid)
                {
                    throw new FileNotFoundException($"File PDF hóa đơn không hợp lệ hoặc không tồn tại: '{pdfPath}'");
                }

                if (!string.IsNullOrWhiteSpace(pdfPath) && File.Exists(pdfPath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        // Lỗi mở viewer ngoài không làm mất trạng thái tạo PDF thành công
                        Debug.WriteLine($"[ReceiptViewModel] Lỗi mở trình đọc PDF: {ex.Message}");
                    }
                }

                _pdfCompleted = true;
                IsPdfGenerationError = false;
                CanRetryPrint = false;
                PdfErrorMessage = string.Empty;
                RaisePdfStatePropertiesChanged();

                // 3. Khởi tạo Timer 5 giây CHỈ SAU KHI hóa đơn đã hiển thị & in/PDF thành công
                StartAutoExitTimer();
            }
            catch (OperationCanceledException)
            {
                // OperationCanceledException do Cleanup/Cancel phải được nuốt im lặng tuyệt đối
                return;
            }
            catch (Exception ex)
            {
                // GUARD TRONG NHÁNH CATCH: kiểm tra trước MỌI side-effect trong catch
                if (_isCleanedUp || _cts == null || token.IsCancellationRequested || requestId != _currentRequestId)
                {
                    return;
                }

                // Ghi log an toàn không chứa PII (chỉ log loại exception và message)
                Debug.WriteLine($"[ReceiptViewModel] Lỗi xử lý tạo PDF hóa đơn: {ex.GetType().Name} - {ex.Message}");

                _pdfCompleted = false;
                IsPdfGenerationError = true;
                EvaluatePdfRetryLimit("Không thể tạo file PDF hóa đơn. Vui lòng bấm Thử lại hoặc liên hệ nhân viên.");
                RaisePdfStatePropertiesChanged();
            }
            finally
            {
                _isPrinting = false;
                if (!_isCleanedUp && _cts != null && !token.IsCancellationRequested && requestId == _currentRequestId)
                {
                    RaisePdfStatePropertiesChanged();
                }
            }
        }

        private void EvaluatePdfRetryLimit(string baseMessage)
        {
            _pdfRetryCount++;
            if (_pdfRetryCount >= 3)
            {
                CanRetryPrint = false;
                PdfErrorMessage = "Đã thử lại tạo/in PDF 3 lần không thành công. Quý khách vui lòng liên hệ nhân viên tại quầy để in hóa đơn.";
            }
            else
            {
                CanRetryPrint = true;
                PdfErrorMessage = $"{baseMessage} (Lần {_pdfRetryCount}/3)";
            }
        }

        private void ExecuteRetryPrint()
        {
            if (CanRetryPrint && !_isPrinting && !_isCleanedUp && Receipt != null)
            {
                if (_cts == null || _cts.IsCancellationRequested)
                {
                    _cts = new CancellationTokenSource();
                }
                _ = ProcessPrintAndStartTimerAsync(Receipt);
            }
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

                // Điều hướng về SearchPage thay vì IdlePage theo Issue #38
                _navigationService.Navigate<SearchPage>();

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
        /// Đảm bảo tính idempotent: gọi nhiều lần an toàn, không ném exception.
        /// </summary>
        public void Cleanup()
        {
            _isCleanedUp = true;

            if (_cts != null)
            {
                try
                {
                    _cts.Cancel();
                    _cts.Dispose();
                }
                catch
                {
                    // Nuốt lỗi an toàn khi dispose
                }
                _cts = null;
            }

            StopTimer();
            CloseMockDialog();
        }
    }
}
