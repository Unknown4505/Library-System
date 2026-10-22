using BookKiosk.Domain.Enums;

namespace BookKiosk.Application.DTOs.Promotions;

/// <summary>
/// Full replacement contract for a promotion and its type-specific details.
/// ProductDiscount details use replace-all semantics.
/// </summary>
public sealed class UpdatePromotionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PromotionType PromotionType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public PromotionOrderDiscountDto? OrderDiscount { get; set; }
    public List<PromotionProductDiscountDto> ProductDiscounts { get; set; } = new();
}
