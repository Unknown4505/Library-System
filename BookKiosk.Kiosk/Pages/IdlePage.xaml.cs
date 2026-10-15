using System.Windows.Controls;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Pages
{
    public partial class IdlePage : Page
    {
        // Constructor dành riêng cho WPF Designer
        public IdlePage()
        {
            InitializeComponent();
        }

        // Constructor tiêm DI
        public IdlePage(IdleViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
