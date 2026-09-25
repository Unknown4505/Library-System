using System;
using System.Collections.Generic;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Domain.Entities;

public class Promotion : BaseEntity
{
    public int PromotionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PromotionType PromotionType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    public PromotionOrderDiscount? OrderDiscount { get; set; }
    public ICollection<PromotionProductDiscount> ProductDiscounts { get; set; } = new List<PromotionProductDiscount>();
}