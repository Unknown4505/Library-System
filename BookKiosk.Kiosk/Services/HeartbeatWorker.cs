using System;
using System.Diagnostics;
using System.Timers;
using BookKiosk.Kiosk.Services.Api;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BookKiosk.Kiosk.Services
{
    public class HeartbeatWorker
    {
        private readonly IBookKioskApiClient _apiClient;
        private readonly IConfiguration _configuration;
        private readonly System.Timers.Timer _timer;

        public HeartbeatWorker(IBookKioskApiClient apiClient, IConfiguration configuration)
        {
            _apiClient = apiClient;
            _configuration = configuration;
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
                int kioskId = _configuration.GetValue<int>("KioskSettings:KioskId", 1);
                if (kioskId <= 0) kioskId = 1;

                var result = await _apiClient.SendHeartbeatAsync(kioskId);
                if (result != null && !result.Success)
                {
                    Debug.WriteLine($"[Heartbeat] Server từ chối heartbeat: {result.Code} - {result.Message}");
                }
            }
            catch (Exception ex)
            {
                // Bắt lỗi kết nối/backend sập để không crash app
                Debug.WriteLine($"[Heartbeat] Lỗi kết nối API: {ex.Message}");
            }
        }
    }
}
