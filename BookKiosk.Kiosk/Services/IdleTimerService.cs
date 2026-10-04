using System;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using BookKiosk.Kiosk.Pages;

namespace BookKiosk.Kiosk.Services
{
    public class IdleTimerService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DispatcherTimer _timer;

        public IdleTimerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _timer.Tick += Timer_Tick;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void ResetTimer()
        {
            // Reset timer bằng cách dừng rồi chạy lại
            _timer.Stop();
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _timer.Stop(); // Tạm dừng timer
            
            // Resolve NavigationService từ DI container để chuyển trang
            var navigationService = _serviceProvider.GetRequiredService<NavigationService>();
            
            // Đã tạo class IdlePage, tiến hành điều hướng
            navigationService.Navigate<IdlePage>();
        }
    }
}
