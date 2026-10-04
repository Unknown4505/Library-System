using System.Windows.Controls;

namespace BookKiosk.Kiosk.Pages
{
    public partial class MemberPage : Page
    {
        public MemberPage()
        {
            InitializeComponent();
        }

        public MemberPage(ViewModels.MemberViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void Numpad_EnterClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MemberViewModel vm && vm.ConfirmCommand.CanExecute(null))
            {
                vm.ConfirmCommand.Execute(null);
            }
        }
    }
}
