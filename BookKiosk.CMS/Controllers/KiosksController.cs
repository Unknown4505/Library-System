using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.ViewModels.Kiosks;

namespace BookKiosk.CMS.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Kiosks")]
    public class KiosksController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<KiosksController> _logger;

        public KiosksController(
            IConfiguration configuration,
            ILogger<KiosksController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
        {
            bool useMock = _configuration.GetValue<bool>("Cms:UseMockApi");

            var viewModel = new KioskMonitoringViewModel
            {
                UseMockApi = useMock
            };

            if (useMock)
            {
                // Dữ liệu mô phỏng để test layout FE khi bật cờ UseMockApi: true trong môi trường Dev
                viewModel.IsBackendAvailable = true;
                viewModel.TotalKiosks = 3;
                viewModel.OnlineCount = 2;
                viewModel.WarningCount = 1;
                viewModel.OfflineCount = 0;
                viewModel.Kiosks = new List<KioskDeviceItem>
                {
                    new()
                    {
                        KioskId = "KSK-01",
                        KioskName = "Kiosk Sảnh Chính (Tầng G)",
                        Location = "Cổng vào số 1, Sảnh chính",
                        IpAddress = "192.168.1.101",
                        Status = "Online",
                        PrinterStatus = "Sẵn sàng (Đầy giấy)",
                        ScannerStatus = "Hoạt động",
                        LastHeartbeat = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).AddSeconds(-15)
                    },
                    new()
                    {
                        KioskId = "KSK-02",
                        KioskName = "Kiosk Khu Sách Văn Học (Tầng 1)",
                        Location = "Khu vực B - Kệ 04",
                        IpAddress = "192.168.1.102",
                        Status = "Warning",
                        PrinterStatus = "Cảnh báo (Sắp hết cuộn giấy)",
                        ScannerStatus = "Hoạt động",
                        LastHeartbeat = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).AddSeconds(-45)
                    },
                    new()
                    {
                        KioskId = "KSK-03",
                        KioskName = "Kiosk Khu Manga / Light Novel (Tầng 2)",
                        Location = "Khu vực C - Gần quầy thu ngân",
                        IpAddress = "192.168.1.103",
                        Status = "Online",
                        PrinterStatus = "Sẵn sàng",
                        ScannerStatus = "Hoạt động",
                        LastHeartbeat = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).AddSeconds(-10)
                    }
                };
            }
            else
            {
                // Khi chạy với Backend thật: Backend hiện chưa có endpoint GET /api/kiosks hay GET /api/kiosks/summary (Docs 04 §3.7).
                // Theo đúng nguyên tắc: phần nào thiếu sẽ để trống chuẩn UI/UX, không hiển thị dữ liệu giả như dữ liệu thật.
                viewModel.IsBackendAvailable = false;
                viewModel.TotalKiosks = 0;
                viewModel.OnlineCount = 0;
                viewModel.WarningCount = 0;
                viewModel.OfflineCount = 0;
                viewModel.Kiosks = new List<KioskDeviceItem>();
                viewModel.StatusMessage = "Backend API hiện chưa triển khai endpoint tổng hợp trạng thái thiết bị (KioskStatusSummaryDto theo Docs 04 §3.7). Toàn bộ danh sách thiết bị đang để trống chờ tích hợp.";
            }

            return View(viewModel);
        }
    }
}

