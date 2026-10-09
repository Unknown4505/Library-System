using System;
using System.Diagnostics;
using System.Timers;
using BookKiosk.Kiosk.Services.Api;
using System.Threading.Tasks;

namespace BookKiosk.Kiosk.Services
{
    public class HeartbeatWorker
    {
        private readonly IBookKioskApiClient _apiClient;
        private readonly System.Timers.Timer _timer;

        public HeartbeatWorker(IBookKioskApiClient apiClient)
        {
            _apiClient = apiClient;
            // Interval 60000ms = 60s
            _timer = new System.Timers.Timer(60000);
            _timer.Elapsed += OnTimerElapsed;
        }

        public void Start()
        {
            _timer.Start();
            // Gửi nhịp tim lần đầu ngay khi start (Fire-and-forget)
            _ = SendHeartbeatAsync();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await SendHeartbeatAsync();
        }

        private async Task SendHeartbeatAsync()
        {
            try
            {
                // Gọi API với đầy đủ tham số để không bị lỗi build
                await _apiClient.SendHeartbeatAsync("KIOSK-01");
            }
            catch (Exception ex)
            {
                // Bắt lỗi kết nối/backend sập để không crash app
                Debug.WriteLine($"[Heartbeat] Lỗi kết nối API: {ex.Message}");
            }
        }
    }
}
