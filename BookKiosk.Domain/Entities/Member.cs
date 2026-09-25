namespace BookKiosk.Domain.Entities;

public class Member : BaseEntity
{
    public int MemberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Points { get; set; } = 0;

    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}