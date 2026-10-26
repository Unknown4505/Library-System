using System;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace BookKiosk.Kiosk.Services
{
    public class NavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private Frame? _mainFrame;

        public Type? CurrentPageType { get; private set; }
        public event Action<Type?>? Navigated;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public void Initialize(Frame mainFrame)
        {
            _mainFrame = mainFrame ?? throw new ArgumentNullException(nameof(mainFrame));
            _mainFrame.Navigated -= MainFrame_Navigated;
            _mainFrame.Navigated += MainFrame_Navigated;
            CurrentPageType = _mainFrame.Content?.GetType();
        }

        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            CurrentPageType = e.Content?.GetType();
            Navigated?.Invoke(CurrentPageType);
        }

        public void Navigate<T>(object? parameter = null) where T : Page
        {
            if (_mainFrame == null)
            {
                throw new InvalidOperationException("NavigationService chưa được khởi tạo với Frame.");
            }

            // Phân giải Page thông qua DI Container
            var page = _serviceProvider.GetRequiredService<T>();

            // Nếu ViewModel của trang có kế thừa BaseViewModel, truyền tham số vào
            if (page.DataContext is ViewModels.BaseViewModel baseViewModel)
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

