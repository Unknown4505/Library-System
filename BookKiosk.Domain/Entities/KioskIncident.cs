using System;

namespace BookKiosk.Domain.Entities;

public class KioskIncident : BaseEntity
{
    public int IncidentId { get; set; }
    public int KioskId { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }

    public Kiosk? Kiosk { get; set; }
}