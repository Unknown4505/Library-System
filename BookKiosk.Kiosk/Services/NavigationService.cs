using System;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace BookKiosk.Kiosk.Services
{
    public class NavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private Frame _mainFrame;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public void Initialize(Frame mainFrame)
        {
            _mainFrame = mainFrame ?? throw new ArgumentNullException(nameof(mainFrame));
        }

        public void Navigate<T>(object parameter = null) where T : Page
        {
            if (_mainFrame == null)
            {
                throw new InvalidOperationException("NavigationService chưa được khởi tạo với Frame.");
            }

            // Phân giải Page thông qua DI Container
            var page = _serviceProvider.GetRequiredService<T>();

            // Nếu ViewModel của trang có kế thừa BaseViewModel, truyền tham số vào
            if (parameter != null && page.DataContext is ViewModels.BaseViewModel baseViewModel)
            {
                baseViewModel.Initialize(parameter);
            }

            _mainFrame.Navigate(page);
        }

        public void GoBack()
        {
            if (_mainFrame != null && _mainFrame.CanGoBack)
            {
                _mainFrame.GoBack();
            }
        }
    }
}
