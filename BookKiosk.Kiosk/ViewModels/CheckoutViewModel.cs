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
        private readonly IBookKioskApiClient _api;

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

        public CheckoutViewModel(CartService cartService, NavigationService navigationService, IBookKioskApiClient api)
        {
            _cartService = cartService;
            _navigationService = navigationService;
            _api = api;

            SimulatePaymentSuccessCommand = new RelayCommand(_ => HandlePaymentSuccess());
            CancelCommand = new RelayCommand(_ => ExecuteCancel());
        }

        private void ExecuteCancel()
        {
            StopTimers();
            // Đưa khách về Trang chủ, reset lại luồng mua sắm
            _navigationService.Navigate<SearchPage>();
        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);

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

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _timeRemainingSeconds--;
            UpdateTimeText();

            if (_timeRemainingSeconds <= 0)
            {
                StopTimers();
                _cartService.ClearCart();
                _navigationService.Navigate<IdlePage>();
            }
        }

        private async void PollingTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Gọi API lấy trạng thái đơn hàng
                var status = await _api.GetPaymentStatusAsync(_orderId);
                if (status != null && status.Data != null && status.Data.IsPaid)
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
