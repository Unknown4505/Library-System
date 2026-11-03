using System;
using System.Collections.Generic;

namespace BookKiosk.CMS.ViewModels.Kiosks
{
    public class KioskDeviceItem
    {
        public string KioskId { get; set; } = string.Empty;
        public string KioskName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline"; // Online, Warning, Offline, Maintenance
        public string PrinterStatus { get; set; } = "Unknown";
        public string ScannerStatus { get; set; } = "Unknown";
        public DateTime? LastHeartbeat { get; set; }
    }

    public class KioskMonitoringViewModel
    {
        public bool UseMockApi { get; set; }
        public bool IsBackendAvailable { get; set; }

        public int TotalKiosks { get; set; }
        public int OnlineCount { get; set; }
        public int WarningCount { get; set; }
        public int OfflineCount { get; set; }

        public List<KioskDeviceItem> Kiosks { get; set; } = new();

        public string StatusMessage { get; set; } = string.Empty;
    }
}

