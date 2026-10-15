using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BookKiosk.Kiosk.ViewModels
{
    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        private string _successMessage;
        public string SuccessMessage
        {
            get => _successMessage;
            set
            {
                if (_successMessage != value)
                {
                    _successMessage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSuccess));
                }
            }
        }

        public bool HasSuccess => !string.IsNullOrWhiteSpace(_successMessage);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Khai báo hàm này để nhận tham số từ NavigationService
        public virtual void Initialize(object parameter)
        {
        }
    }
}
