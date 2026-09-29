using System;

namespace BookKiosk.Application.DTOs.Promotions;

public class PromotionDto
{
    public int PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}
