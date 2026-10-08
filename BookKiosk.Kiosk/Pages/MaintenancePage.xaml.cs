using System.Windows.Controls;

namespace BookKiosk.Kiosk.Pages
{
    public partial class MaintenancePage : Page
    {
        public MaintenancePage(ViewModels.MaintenanceViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
