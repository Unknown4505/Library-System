using System.Windows.Controls;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Pages
{
    public partial class BookDetailPage : Page
    {
        public BookDetailPage(BookDetailViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }
    }
}
