using System.Windows.Controls;

namespace BookKiosk.Kiosk.Pages
{
    public partial class MemberPage : Page
    {
        public MemberPage()
        {
            InitializeComponent();
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
