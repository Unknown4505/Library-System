using System.Windows;
using System.Windows.Controls;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Pages
{
    /// <summary>
    /// Trang hiển thị hóa đơn và trạng thái thanh toán trên Kiosk (Task 8.2.1, Issue #29).
    /// </summary>
    public partial class ReceiptPage : Page
    {
        public ReceiptPage()
        {
            InitializeComponent();
        }

        public ReceiptPage(ReceiptViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ReceiptViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }
}
