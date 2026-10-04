using System.Windows.Controls;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Pages
{
    public partial class SearchPage : Page
    {
        public SearchPage(SearchViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
