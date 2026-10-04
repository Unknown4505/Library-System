using System;
using System.Linq;
using System.Windows.Threading;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Kiosk.Controls;

namespace BookKiosk.Kiosk.ViewModels
{
    public class ReceiptViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly NavigationService _navigationService;
        private DispatcherTimer _timer;
        private MockReceiptDialog _mockDialog;

        public ReceiptViewModel(CartService cartService, NavigationService navigationService)
        {
            _cartService = cartService;
            _navigationService = navigationService;
        }

        public override void Initialize(object parameter)
        {
            base.Initialize(parameter);
            
            // Kích hoạt UI Giả lập in hóa đơn đè giữa màn hình
            var cartItems = _cartService.Items.ToList(); // clone danh sách phòng khi clear
            var totalAmount = _cartService.GetTotalAmount();
            
            _mockDialog = new MockReceiptDialog(cartItems, totalAmount);
            _mockDialog.Show();

            // Khởi tạo Timer đếm đúng 5 giây
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            StopTimer();
            
            // Đóng hóa đơn mô phỏng nếu nó còn mở
            if (_mockDialog != null)
            {
                _mockDialog.Close();
                _mockDialog = null;
            }

            // Xóa sạch giỏ hàng (LOGIC BẮT BUỘC)
            _cartService.ClearCart();

            // Về trang chủ (IdlePage) đón khách tiếp theo
            _navigationService.Navigate<IdlePage>();
        }

        public void Cleanup()
        {
            StopTimer();
            if (_mockDialog != null)
            {
                _mockDialog.Close();
                _mockDialog = null;
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
    }
}
