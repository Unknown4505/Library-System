using System;
using System.Collections.Generic;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Domain.Entities;

public class Kiosk : BaseEntity
{
    public int KioskId { get; set; }
    public string KioskName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public KioskStatus Status { get; set; }
    public DateTime? LastPingAt { get; set; }
    
    // Liên kết Kiosk với một Khu vực (Vị trí đặt máy)
    public int? AreaId { get; set; }
    public Area? Area { get; set; }

    public ICollection<KioskIncident> Incidents { get; set; } = new List<KioskIncident>();
}