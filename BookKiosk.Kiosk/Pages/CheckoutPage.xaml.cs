using System.Windows;
using System.Windows.Controls;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Pages
{
    public partial class CheckoutPage : Page
    {
        public CheckoutPage()
        {
            InitializeComponent();
        }

        public CheckoutPage(CheckoutViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Dọn dẹp Timers khi bị văng khỏi trang để giải phóng bộ nhớ
            if (DataContext is CheckoutViewModel vm)
            {
                vm.Cleanup();
            }
        }

        private void QrImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (QrFallbackText != null)
            {
                QrFallbackText.Text = "Không tải được mã QR. Vui lòng thử lại";
                QrFallbackText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }
    }
}
