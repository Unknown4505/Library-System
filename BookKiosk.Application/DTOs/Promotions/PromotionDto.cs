using System;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Application.DTOs.Promotions;

public class PromotionDto
{
    public int PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PromotionType PromotionType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public PromotionOrderDiscountDto? OrderDiscount { get; set; }
    public List<PromotionProductDiscountDto> ProductDiscounts { get; set; } = new();
}
