using System.Windows.Input;
using BookKiosk.Kiosk.Services;

namespace BookKiosk.Kiosk.ViewModels
{
    public class MaintenanceViewModel : BaseViewModel
    {
        private readonly NavigationService _navigationService;

        public ICommand ExitMaintenanceCommand { get; }

        public MaintenanceViewModel(NavigationService navigationService)
        {
            _navigationService = navigationService;
            // Backdoor: Thoát về trang chủ (IdlePage)
            ExitMaintenanceCommand = new RelayCommand(_ => ExecuteExit());
        }

        private void ExecuteExit()
        {
            _navigationService.Navigate<Pages.IdlePage>();
        }
    }
}
