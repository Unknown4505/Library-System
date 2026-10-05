using System.Windows.Input;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Pages;

namespace BookKiosk.Kiosk.ViewModels
{
    public class IdleViewModel : BaseViewModel
    {
        private readonly NavigationService _navigationService;

        public ICommand StartCommand { get; }

        public IdleViewModel(NavigationService navigationService)
        {
            _navigationService = navigationService ?? throw new System.ArgumentNullException(nameof(navigationService));
            
            StartCommand = new RelayCommand(ExecuteStart);
        }

        private void ExecuteStart(object parameter)
        {
            _navigationService.Navigate<SearchPage>();
        }
    }
}
