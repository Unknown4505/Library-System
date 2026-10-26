using System;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using BookKiosk.Kiosk.Pages;

namespace BookKiosk.Kiosk.Services
{
    public class IdleTimerService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly NavigationService _navigationService;
        private readonly DispatcherTimer _timer;
        public bool IsSuspended { get; private set; } = true;

        public IdleTimerService(NavigationService navigationService, IServiceProvider serviceProvider)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _timer.Tick += Timer_Tick;

            _navigationService.Navigated += OnNavigated;
        }

        public IdleTimerService(IServiceProvider serviceProvider)
            : this(serviceProvider.GetRequiredService<NavigationService>(), serviceProvider)
        {
        }

        /// <summary>
        /// Theo quyết định Leader: Inactivity timer 60s CHỈ đếm tại SearchPage.
        /// Tuyệt đối tạm dừng khi ở BookDetailPage, IdlePage, MemberPage, CheckoutPage, ReceiptPage.
        /// </summary>
        private static bool IsEligiblePage(Type? pageType)
        {
            return pageType == typeof(SearchPage);
        }

        public void Start()
        {
            if (IsEligiblePage(_navigationService.CurrentPageType))
            {
                IsSuspended = false;
                _timer.Stop();
                _timer.Start();
            }
            else
            {
                // Cưỡng chế dừng nếu trang hiện tại không thuộc phạm vi đếm (Member, Checkout, Receipt, Idle...)
                IsSuspended = true;
                _timer.Stop();
            }
        }

        public void Stop()
        {
            IsSuspended = true;
            _timer.Stop();
        }

        public void ResetTimer()
        {
            if (IsSuspended || !IsEligiblePage(_navigationService.CurrentPageType)) return;

            // Reset timer bằng cách dừng rồi chạy lại
            _timer.Stop();
            _timer.Start();
        }

        private void OnNavigated(Type? newPageType)
        {
            if (IsEligiblePage(newPageType))
            {
                // Bước vào trang hợp lệ (SearchPage/BookDetailPage): Bắt đầu chu kỳ 60s mới
                IsSuspended = false;
                _timer.Stop();
                _timer.Start();
            }
            else
            {
                // Rời khỏi trang hợp lệ sang Member, Checkout, Receipt, Idle...: Dừng ngay lập tức
                IsSuspended = true;
                _timer.Stop();
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer.Stop();
            
            // Lớp bảo vệ 2: nếu không còn ở trang hợp lệ thì bỏ qua
            if (IsSuspended || !IsEligiblePage(_navigationService.CurrentPageType))
            {
                return;
            }

            IsSuspended = true; // Đảm bảo chuyển Idle đúng 1 lần, không lặp lại
            
            // Giữ nguyên hành vi hiện hành: chuyển về IdlePage (không tự xóa giỏ)
            _navigationService.Navigate<IdlePage>();
        }
    }
}

