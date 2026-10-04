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
    }
}
