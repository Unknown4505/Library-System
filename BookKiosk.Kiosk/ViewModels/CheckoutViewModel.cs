using System;
using System.Windows.Input;
using System.Windows.Threading;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Kiosk.Services.Api;

namespace BookKiosk.Kiosk.ViewModels
{
    public class CheckoutViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly NavigationService _navigationService;
        private readonly IBookKioskApiClient _apiClient;
        private readonly IdleTimerService _idleTimerService;

        private DispatcherTimer _countdownTimer;
        private DispatcherTimer _pollingTimer;
        private int _timeRemainingSeconds = 180; // 3 phút
        private int _orderId;

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            set { _totalAmount = value; OnPropertyChanged(); }
        }

        public System.Collections.ObjectModel.ObservableCollection<Models.CartItemModel> CartItems => _cartService.Items;
        
        public decimal SubTotal => _cartService.GetTotalAmount();
        
        private decimal _discount;
        public decimal Discount
        {
            get => _discount;
            set { _discount = value; OnPropertyChanged(); }
        }

        private string _timeRemainingText = "03:00";
        public string TimeRemainingText
        {
            get => _timeRemainingText;
            set { _timeRemainingText = value; OnPropertyChanged(); }
        }

        public ICommand SimulatePaymentSuccessCommand { get; }
        public ICommand CancelCommand { get; }

        public CheckoutViewModel(CartService cartService, NavigationService navigationService, IBookKioskApiClient apiClient, IdleTimerService idleTimerService)
        {
            _cartService = cartService;
            _navigationService = navigationService;
            _apiClient = apiClient;
            _idleTimerService = idleTimerService;

            SimulatePaymentSuccessCommand = new RelayCommand(_ => HandlePaymentSuccess());
            CancelCommand = new RelayCommand(_ => ExecuteCancel());
        }

        private async void ExecuteCancel()
        {
            if (IsLoading) return;
            IsLoading = true;
            StopTimers();
            
            try
            {
                // Hủy đơn hàng an toàn (nhả tồn kho)
                await _apiClient.CancelOrderAsync(_orderId);
            }
            finally
            {
                IsLoading = false;
                // Đưa khách về Trang chủ, reset lại luồng mua sắm
                _navigationService.Navigate<SearchPage>();
            }
        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            _idleTimerService.Stop(); // Tạm dừng Idle Timer toàn cục để không đá văng người dùng khi đang thanh toán

            if (parameter is CheckoutParameter p)
            {
                TotalAmount = p.TotalAmount;
                Discount = p.PointsUsed * 1000m;
                _orderId = p.OrderId;
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

        private async void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _timeRemainingSeconds--;
            UpdateTimeText();

            if (_timeRemainingSeconds <= 0)
            {
                if (IsLoading) return;
                IsLoading = true;
                StopTimers();
                
                try
                {
                    // Hủy đơn hàng an toàn do hết giờ (nhả tồn kho)
                    await _apiClient.CancelOrderAsync(_orderId);
                }
                finally
                {
                    IsLoading = false;
                    _cartService.ClearCart();
                    _navigationService.Navigate<IdlePage>();
                }
            }
        }

        private async void PollingTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Gọi API lấy trạng thái đơn hàng
                var response = await _apiClient.GetPaymentStatusAsync(_orderId);
                if (response != null && response.Success && response.Data != null && response.Data.Status == "Paid")
                {
                    HandlePaymentSuccess();
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
            StopTimers();
            // Điều hướng sang màn hình In Hóa đơn
            _navigationService.Navigate<ReceiptPage>();
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
