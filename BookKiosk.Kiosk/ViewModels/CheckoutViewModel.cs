using System;
using System.Windows.Input;
using System.Windows.Threading;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Kiosk.Services.Api;
using BookKiosk.Kiosk.Models;

namespace BookKiosk.Kiosk.ViewModels
{
    public class CheckoutViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly NavigationService _navigationService;
        private readonly IBookKioskApiClient _apiClient;
        private readonly IdleTimerService _idleTimerService;

        private DispatcherTimer? _countdownTimer;
        private DispatcherTimer? _pollingTimer;
        private int _timeRemainingSeconds = 180; // 3 phút
        private int _orderId;
        private string _orderCode = string.Empty;
        private bool _isProcessingFinalState = false;

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            set { _totalAmount = value; OnPropertyChanged(); }
        }

        public System.Collections.ObjectModel.ObservableCollection<CartItemModel> CartItems => _cartService.Items;
        
        public decimal SubTotal => _cartService.GetTotalAmount();
        
        private decimal _promotionDiscount;
        public decimal PromotionDiscount
        {
            get => _promotionDiscount;
            set
            {
                _promotionDiscount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Discount));
            }
        }

        private decimal _pointsDiscount;
        public decimal PointsDiscount
        {
            get => _pointsDiscount;
            set
            {
                _pointsDiscount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Discount));
            }
        }

        public decimal Discount => PromotionDiscount + PointsDiscount;

        private string _timeRemainingText = "03:00";
        public string TimeRemainingText
        {
            get => _timeRemainingText;
            set { _timeRemainingText = value; OnPropertyChanged(); }
        }

        private string _sepayQrCodeUrl = string.Empty;
        public string SepayQrCodeUrl
        {
            get => _sepayQrCodeUrl;
            set { _sepayQrCodeUrl = value; OnPropertyChanged(); }
        }

        public bool IsDevMode =>
#if DEBUG
            true;
#else
            false;
#endif

        public ICommand? SimulatePaymentSuccessCommand { get; }
        public ICommand CancelCommand { get; }

        public CheckoutViewModel(CartService cartService, NavigationService navigationService, IBookKioskApiClient apiClient, IdleTimerService idleTimerService)
        {
            _cartService = cartService;
            _navigationService = navigationService;
            _apiClient = apiClient;
            _idleTimerService = idleTimerService;

#if DEBUG
            SimulatePaymentSuccessCommand = new RelayCommand(_ => SimulatePaymentSuccess());
#else
            SimulatePaymentSuccessCommand = null;
#endif
            CancelCommand = new RelayCommand(_ => ExecuteCancel());
        }

#if DEBUG
        private async void SimulatePaymentSuccess()
        {
            if (IsLoading || _isProcessingFinalState) return;
            IsLoading = true;
            try
            {
                var response = await _apiClient.DevSimulatePaymentSuccessAsync(_orderCode);
                if (response == null || !response.Success)
                {
                    ErrorMessage = "Lỗi khi gọi mô phỏng DEV: " + (response?.Message ?? "Mất kết nối máy chủ");
                }
                else
                {
                    ErrorMessage = "Đã gửi yêu cầu mô phỏng DEV thành công, đang chờ phản hồi...";
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
#endif

        private async void ExecuteCancel()
        {
            if (IsLoading || _isProcessingFinalState) return;
            IsLoading = true;
            
            try
            {
                // 1. Reconcile trạng thái trước khi hủy: kiểm tra xem khách vừa thanh toán thành công hay chưa
                var statusCheck = await _apiClient.GetPaymentStatusAsync(_orderId);
                if (statusCheck != null && statusCheck.Success && statusCheck.Data != null && statusCheck.Data.Status == "Paid")
                {
                    HandlePaymentSuccess();
                    return;
                }

                // 2. Hủy đơn hàng an toàn (nhả tồn kho)
                var result = await _apiClient.CancelOrderAsync(_orderId);
                if (result == null || !result.Success)
                {
                    ErrorMessage = result?.Message ?? "Hủy đơn hàng thất bại do lỗi mạng. Vui lòng thử lại.";
                    return; // KHÔNG StopTimers(), để timers tiếp tục hoạt động cho phép thử lại hoặc polling
                }
                
                // 3. Đã hủy thành công từ BE
                _isProcessingFinalState = true;
                StopTimers();
                _navigationService.Navigate<SearchPage>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            _idleTimerService.Stop(); // Tạm dừng Idle Timer toàn cục để không đá văng người dùng khi đang thanh toán
            _isProcessingFinalState = false;

            if (parameter is CheckoutParameter p)
            {
                TotalAmount = p.TotalAmount;
                PromotionDiscount = p.DiscountAmount;
                PointsDiscount = p.PointsUsedAmount;
                _orderId = p.OrderId;
                _orderCode = p.OrderCode;
                SepayQrCodeUrl = p.SepayQrCodeUrl;
            }

            _timeRemainingSeconds = 180;
            UpdateTimeText();

            // Khởi tạo Timer 1: Đếm ngược UI (3 phút)
            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();

            // Khởi tạo Timer 2: Polling API 3s/lần
            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _pollingTimer.Tick += PollingTimer_Tick;
            _pollingTimer.Start();
        }

        private async void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _timeRemainingSeconds--;
            UpdateTimeText();

            if (_timeRemainingSeconds <= 0)
            {
                if (IsLoading || _isProcessingFinalState) return;
                IsLoading = true;
                
                try
                {
                    // 1. Kiểm tra trạng thái xem khách vừa thanh toán hay chưa
                    var statusCheck = await _apiClient.GetPaymentStatusAsync(_orderId);
                    if (statusCheck != null && statusCheck.Success && statusCheck.Data != null && statusCheck.Data.Status == "Paid")
                    {
                        HandlePaymentSuccess();
                        return;
                    }

                    // 2. Hủy đơn hàng do hết giờ (nhả tồn kho)
                    var result = await _apiClient.CancelOrderAsync(_orderId);
                    if (result == null || !result.Success)
                    {
                        ErrorMessage = "Hết giờ nhưng hủy đơn thất bại do lỗi mạng. Vui lòng ấn HỦY BỎ để thử lại.";
                        return; // KHÔNG StopTimers() để khách có thể thao tác lại
                    }

                    _isProcessingFinalState = true;
                    StopTimers();
                    _cartService.ClearCart();
                    _navigationService.Navigate<IdlePage>();
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        private async void PollingTimer_Tick(object? sender, EventArgs e)
        {
            if (_isProcessingFinalState) return;

            try
            {
                // Gọi API lấy trạng thái đơn hàng
                var response = await _apiClient.GetPaymentStatusAsync(_orderId);
                if (response != null && response.Success && response.Data != null)
                {
                    if (response.Data.Status == "Paid")
                    {
                        HandlePaymentSuccess();
                    }
                    else if (response.Data.Status == "Cancelled")
                    {
                        _isProcessingFinalState = true;
                        StopTimers();
                        ErrorMessage = "Đơn hàng đã bị hủy trên hệ thống.";
                        _cartService.ClearCart();
                        _navigationService.Navigate<SearchPage>();
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi tạm thời (mạng rớt, timeout) để timer tick lần sau tiếp tục chạy
            }
        }

        private void UpdateTimeText()
        {
            TimeSpan time = TimeSpan.FromSeconds(_timeRemainingSeconds);
            TimeRemainingText = time.ToString(@"mm\:ss");
        }

        private void HandlePaymentSuccess()
        {
            if (_isProcessingFinalState) return;
            _isProcessingFinalState = true;

            StopTimers();

            var receiptParam = new ReceiptParameter
            {
                OrderId = _orderId,
                OrderCode = _orderCode,
                TotalAmount = TotalAmount,
                PromotionDiscount = PromotionDiscount,
                PointsDiscount = PointsDiscount,
                SubTotal = SubTotal
            };

            // Điều hướng sang màn hình In Hóa đơn mang đầy đủ Order metadata
            _navigationService.Navigate<ReceiptPage>(receiptParam);
        }

        // Hàm này sẽ được gọi từ Code-behind Page_Unloaded để dọn dẹp bộ nhớ (Tránh rò rỉ Memory Leak)
        public void Cleanup()
        {
            StopTimers();
            _idleTimerService.Start(); // Bật lại Idle Timer
        }

        private void StopTimers()
        {
            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Tick -= CountdownTimer_Tick;
                _countdownTimer = null;
            }
            if (_pollingTimer != null)
            {
                _pollingTimer.Stop();
                _pollingTimer.Tick -= PollingTimer_Tick;
                _pollingTimer = null;
            }
        }
    }
}
