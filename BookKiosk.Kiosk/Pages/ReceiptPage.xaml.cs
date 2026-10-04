using System.Windows;
using System.Windows.Controls;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Pages
{
    public partial class ReceiptPage : Page
    {
        public ReceiptPage()
        {
            InitializeComponent();
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
