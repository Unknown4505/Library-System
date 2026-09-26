using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Domain.Entities;

[Index(nameof(OrderCode), IsUnique = true)]
public class Order : BaseEntity
{
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public SaleChannel SaleChannel { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    
    public int? UserId { get; set; }
    public int? MemberId { get; set; }
    public int? PromotionId { get; set; }
    
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public int PointsUsed { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? CompletedAt { get; set; }

    public User? CreatedByUser { get; set; }
    public Member? Member { get; set; }
    public Promotion? Promotion { get; set; }

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
}