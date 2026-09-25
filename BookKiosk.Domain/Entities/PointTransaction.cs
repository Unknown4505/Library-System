using BookKiosk.Domain.Enums;

namespace BookKiosk.Domain.Entities;

public class PointTransaction : BaseEntity
{
    public int PointTransactionId { get; set; }
    public int MemberId { get; set; }
    public int? OrderId { get; set; }
    public PointTransactionType Type { get; set; }
    public int Points { get; set; }
    public string? Description { get; set; }

    public Member? Member { get; set; }
    public Order? Order { get; set; }
}